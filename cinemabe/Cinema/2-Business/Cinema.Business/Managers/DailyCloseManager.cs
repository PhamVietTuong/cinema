using Cinema.Business.Contracts;
using Cinema.Business.DTO.BoxOffice;
using Cinema.Business.Helpers;
using Cinema.Data.Contracts;
using Cinema.Data.Enums;
using Microsoft.Extensions.Configuration;

namespace Cinema.Business.Managers;

public class DailyCloseManager : IDailyCloseManager
{
    private const string _dayCutoffHourKey = "Business:DayCutoffHour";
    private const string _varianceToleranceKey = "CashDrawer:VarianceTolerance";

    private readonly IApplicationUnitOfWork _uow;
    private readonly IConfiguration _config;
    private readonly TimeProvider _clock;

    public DailyCloseManager(IApplicationUnitOfWork uow, IConfiguration config, TimeProvider clock)
    {
        _uow = uow;
        _config = config;
        _clock = clock;
    }

    public async Task<DailyCloseDTO> GetDailyCloseAsync(Guid theaterId, DateTime? businessDate)
    {
        var cutoff = int.TryParse(_config[_dayCutoffHourKey], out var configured) && configured >= 0 && configured < 24
            ? configured
            : BusinessCalendar.DefaultCutoffHour;
        var date = (businessDate ?? BusinessCalendar.BusinessDateOf(_clock.GetUtcNow().UtcDateTime, cutoff)).Date;
        var (from, to) = BusinessCalendar.WindowOf(date, cutoff);

        // Six independent aggregate queries (no per-row work), whatever the volume of the day.
        var tenders = await _uow.AfterSalesStore.GetTenderTotalsAsync(theaterId, from, to);
        var refunded = await _uow.AfterSalesStore.GetRefundedInvoicesAsync(theaterId, from, to);
        var volume = await _uow.AfterSalesStore.GetSalesVolumeAsync(theaterId, from, to);
        var comps = await _uow.AfterSalesStore.GetAuditTotalsAsync(theaterId, AuditAction.Compensation, from, to);
        var sessions = await _uow.AfterSalesStore.GetDrawerSessionsAsync(theaterId, from, to);

        var tolerance = double.TryParse(_config[_varianceToleranceKey], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var t) ? t : 0;

        // A refund caused by an exchange only gives back the price difference in the customer's favour (the rest of the
        // money stays in the replacement invoice), so it counts as an exchange, not as a full refund.
        var exchanges = refunded.Where(r => r.ReplacementFinalAmount.HasValue).ToList();
        var plainRefunds = refunded.Where(r => !r.ReplacementFinalAmount.HasValue).ToList();
        var refundAmount = plainRefunds.Sum(r => Whole(r.FinalAmount))
            + exchanges.Sum(r => Math.Max(Whole(r.FinalAmount) - Whole(r.ReplacementFinalAmount!.Value), 0));

        var tenderRows = tenders
            .OrderBy(x => x.Method)
            .Select(x => new DailyCloseTenderDTO { Method = x.Method, Amount = Whole(x.Amount), Count = x.Count })
            .ToList();
        var moneyCollected = tenderRows
            .Where(x => x.Method is PaymentTender.Cash or PaymentTender.Card or PaymentTender.QrWallet or PaymentTender.Online)
            .Sum(x => x.Amount);

        var drawers = sessions.Select(s => new DailyCloseDrawerDTO
        {
            SessionId = s.Id,
            TerminalName = s.TerminalName,
            UserId = s.UserId,
            UserName = s.UserName,
            Status = s.Status,
            OpenedAt = s.OpenedAt,
            ClosedAt = s.ClosedAt,
            OpeningFloat = s.OpeningFloat,
            ExpectedCash = s.ExpectedCash ?? Whole(s.MovementTotal),
            CountedCash = s.CountedCash,
            Variance = s.Variance,
        }).ToList();

        return new DailyCloseDTO
        {
            TheaterId = theaterId,
            BusinessDate = date,
            FromUtc = from,
            ToUtc = to,
            Tenders = tenderRows,
            PaymentsTotal = tenderRows.Sum(x => x.Amount),
            MoneyCollected = moneyCollected,
            RefundCount = plainRefunds.Count,
            RefundAmount = refundAmount,
            ExchangeCount = exchanges.Count,
            CompCount = comps.Count,
            CompAmount = comps.Amount,
            NetCollected = moneyCollected - refundAmount,
            TicketsSold = volume.TicketsSold,
            FoodItemsSold = volume.FoodItemsSold,
            FoodRevenue = Whole(volume.FoodRevenue),
            Drawers = drawers,
            UnreconciledDrawerCount = sessions.Count(s => s.Status == CashDrawerStatus.Closed && Math.Abs(s.Variance ?? 0) > tolerance),
            TotalVariance = drawers.Where(d => d.Status != CashDrawerStatus.Open).Sum(d => d.Variance ?? 0),
        };
    }

    private static double Whole(double value)
    {
        return Math.Round(value, 0, MidpointRounding.AwayFromZero);
    }
}
