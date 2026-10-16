using Cinema.Business.Contracts;
using Cinema.Business.DTO.Auth;
using Cinema.Business.DTO.BoxOffice;
using Cinema.Data.Contracts;
using Cinema.Data.Enums;

namespace Cinema.Business.Managers;

public class BoxOfficeManager : IBoxOfficeManager
{
    private readonly IApplicationUnitOfWork _uow;
    private readonly IBookingManager _booking;
    private readonly IManagerOverrideService _overrides;

    public BoxOfficeManager(IApplicationUnitOfWork uow, IBookingManager booking, IManagerOverrideService overrides)
    {
        _uow = uow;
        _booking = booking;
        _overrides = overrides;
    }

    public async Task<CounterQuoteDTO> QuoteAsync(Guid theaterId, CounterSaleRequest request)
    {
        await ValidateRequestAsync(request);
        return await _booking.QuoteCounterAsync(new CounterSaleContext { TheaterId = theaterId, Request = request });
    }

    public async Task<CounterSaleResultDTO> SellAsync(Guid theaterId, Guid staffUserId, CounterSaleRequest request)
    {
        await ValidateRequestAsync(request);

        // The approval is verified BEFORE the sale's own transaction opens: a failed PIN attempt is persisted by the
        // override service and must survive even though the sale is rejected.
        Guid? approverUserId = null;
        if (request.Seats.Any(s => s.OverrideUnitPrice.HasValue) || request.Foods.Any(f => f.OverrideUnitPrice.HasValue))
        {
            approverUserId = await _overrides.VerifyAsync(theaterId, staffUserId, request.Override, AuditAction.PriceOverride);
        }

        return await _booking.SellAtCounterAsync(new CounterSaleContext
        {
            TheaterId = theaterId,
            StaffUserId = staffUserId,
            ApproverUserId = approverUserId,
            Request = request,
        });
    }

    private async Task ValidateRequestAsync(CounterSaleRequest request)
    {
        if (request.Seats.Count == 0 && request.Foods.Count == 0)
        {
            throw new InvalidOperationException("A sale must include at least one seat or food item.");
        }
        if (request.Seats.Count > 0 && (request.ShowTimeId is null || request.RoomId is null))
        {
            throw new InvalidOperationException("A showtime and room are required to sell seats.");
        }
        if (request.Seats.Select(s => s.SeatId).Distinct().Count() != request.Seats.Count)
        {
            throw new InvalidOperationException("Each seat can be listed only once.");
        }
        if (request.Foods.Select(f => f.FoodAndDrinkId).Distinct().Count() != request.Foods.Count)
        {
            throw new InvalidOperationException("Each food item can be listed only once; use its quantity.");
        }
        if (request.PointsToRedeem > 0 && request.CustomerUserId is null)
        {
            throw new InvalidOperationException("Attach a member to redeem points.");
        }
        if (request.CustomerUserId is Guid customerId && !await _uow.UserStore.ExistsAsync(u => u.Id == customerId))
        {
            throw new KeyNotFoundException("The selected customer was not found.");
        }
    }

    // ── Lookups ─────────────────────────────────────────────────────────────────

    public async Task<List<CounterShowtimeDTO>> GetShowtimesTodayAsync(Guid theaterId, DateTime? date)
    {
        // Showtimes are stored in server local time, so the business day is a local calendar day.
        var now = DateTime.Now;
        var day = (date ?? now).Date;
        var rows = await _uow.ShowTimeStore.GetByTheaterAndRangeAsync(theaterId, day, day.AddDays(1));
        return rows.Select(r => new CounterShowtimeDTO
        {
            ShowTimeId = r.ShowTimeId,
            RoomId = r.RoomId,
            RoomName = r.RoomName,
            MovieId = r.MovieId,
            MovieTitle = r.MovieTitle,
            StartTime = r.StartTime,
            EndTime = r.EndTime,
            ProjectionForm = r.ProjectionForm,
            HasEnded = r.EndTime <= now,
        }).ToList();
    }

    public async Task<CounterCustomerDTO> FindCustomerAsync(string phone)
    {
        var user = await _uow.UserStore.GetByPhoneAsync(phone.Trim());
        // Only customer accounts can be attached to a sale (never a staff account that shares the phone lookup).
        if (user is null || user.UserType?.Name != RoleNames.Customer)
        {
            throw new KeyNotFoundException("No customer with that phone number.");
        }
        var membership = user.MemberShip;
        return new CounterCustomerDTO
        {
            Id = user.Id,
            Name = user.Name,
            Phone = user.Phone,
            Points = user.Points,
            MemberShipName = membership?.Name,
            DiscountPercent = membership?.DiscountPercent ?? 0,
        };
    }
}
