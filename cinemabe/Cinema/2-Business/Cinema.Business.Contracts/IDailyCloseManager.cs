using Cinema.Business.DTO.BoxOffice;

namespace Cinema.Business.Contracts;

/// <summary>End-of-day cash close of one theater. The business day follows decision D4: Asia/Ho_Chi_Minh local time
/// with the <c>Business:DayCutoffHour</c> (default 6) cut-off.</summary>
public interface IDailyCloseManager
{
    /// <summary>Totals by tender (the sum of InvoicePayment rows paid in the day), refunds, exchanges, comps, tickets and
    /// food sold, and every drawer session of the day with its variance.</summary>
    Task<DailyCloseDTO> GetDailyCloseAsync(Guid theaterId, DateTime? businessDate);
}
