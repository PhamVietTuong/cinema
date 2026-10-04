using Cinema.Business.Contracts;
using Cinema.Business.DTO.Auth;
using Cinema.Business.DTO.BoxOffice;
using Cinema.Business.DTO.Staff;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Cinema.Data.Enums;

namespace Cinema.Business.Managers;

public class BoxOfficeManager : IBoxOfficeManager
{
    private const int _recentMovementCount = 20;

    private readonly IApplicationUnitOfWork _uow;
    private readonly IBookingManager _booking;
    private readonly IManagerOverrideService _overrides;
    private readonly IAuditLogger _audit;

    public BoxOfficeManager(IApplicationUnitOfWork uow, IBookingManager booking, IManagerOverrideService overrides, IAuditLogger audit)
    {
        _uow = uow;
        _booking = booking;
        _overrides = overrides;
        _audit = audit;
    }

    public async Task<CounterQuoteDTO> QuoteAsync(Guid theaterId, CounterSaleRequest request)
    {
        await ValidateRequestAsync(request);
        return await _booking.QuoteCounterAsync(new CounterSaleContext { TheaterId = theaterId, Request = request });
    }

    public async Task<CounterSaleResultDTO> SellAsync(Guid theaterId, Guid staffUserId, CounterSaleRequest request)
    {
        await ValidateRequestAsync(request);

        // The drawer is looked up first so a cashier who forgot to open it learns that before anything else.
        var drawer = await _uow.CashDrawerStore.GetOpenForUserAsync(staffUserId);
        if (drawer is not null && drawer.TheaterId != theaterId)
        {
            drawer = null;
        }
        if (request.Tenders.Any(t => t.Method == PaymentTender.Cash) && drawer is null)
        {
            throw new InvalidOperationException("Open a cash drawer before taking cash.");
        }

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
            CashDrawerSessionId = drawer?.Id,
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

    // ── Cash drawer ─────────────────────────────────────────────────────────────

    public async Task<CashDrawerDTO> OpenDrawerAsync(Guid theaterId, Guid staffUserId, OpenDrawerRequest request)
    {
        var terminal = request.TerminalName.Trim();
        if (terminal.Length == 0)
        {
            throw new InvalidOperationException("A terminal name is required.");
        }
        var openingFloat = Whole(request.OpeningFloat);
        if (openingFloat < 0)
        {
            throw new InvalidOperationException("The opening float cannot be negative.");
        }
        if (await _uow.CashDrawerStore.GetOpenForUserAsync(staffUserId) is not null)
        {
            throw new InvalidOperationException("You already have an open cash drawer.");
        }
        if (await _uow.CashDrawerStore.IsTerminalInUseAsync(theaterId, terminal))
        {
            throw new InvalidOperationException("This terminal already has an open cash drawer.");
        }

        var session = new CashDrawerSession
        {
            TheaterId = theaterId,
            UserId = staffUserId,
            TerminalName = terminal,
            Status = CashDrawerStatus.Open,
            OpenedAt = DateTime.UtcNow,
            OpeningFloat = openingFloat,
        };
        _uow.CashDrawerStore.StageSession(session);
        _uow.CashDrawerStore.StageMovement(new CashMovement
        {
            CashDrawerSessionId = session.Id,
            TheaterId = theaterId,
            Type = CashMovementType.OpeningFloat,
            Amount = openingFloat,
            UserId = staffUserId,
        });
        await _uow.SaveChangesAsync();

        return await ToDrawerDtoAsync(session);
    }

    public async Task<CashDrawerDTO> GetMyDrawerAsync(Guid theaterId, Guid staffUserId)
    {
        var drawer = await _uow.CashDrawerStore.GetOpenForUserAsync(staffUserId);
        if (drawer is null || drawer.TheaterId != theaterId)
        {
            return new CashDrawerDTO { IsOpen = false };
        }
        return await ToDrawerDtoAsync(drawer);
    }

    public async Task<CashDrawerDTO> PayInOutAsync(Guid theaterId, Guid staffUserId, PayInOutRequest request)
    {
        if (request.Type != CashMovementType.PayIn && request.Type != CashMovementType.PayOut)
        {
            throw new InvalidOperationException("Only a pay-in or a pay-out can be recorded here.");
        }
        var amount = Whole(request.Amount);
        if (amount <= 0)
        {
            throw new InvalidOperationException("The amount must be greater than zero.");
        }
        var note = request.Note.Trim();
        if (note.Length == 0)
        {
            throw new InvalidOperationException("A note is required.");
        }

        var drawer = await _uow.CashDrawerStore.GetOpenForUserAsync(staffUserId);
        if (drawer is null || drawer.TheaterId != theaterId)
        {
            throw new InvalidOperationException("Open a cash drawer first.");
        }

        // Verified BEFORE the write so a failed attempt is persisted by the override service on its own.
        Guid? approverUserId = null;
        if (request.Type == CashMovementType.PayOut)
        {
            approverUserId = await _overrides.VerifyAsync(theaterId, staffUserId, request.Override, AuditAction.CashPayOut);
            var totals = await _uow.CashDrawerStore.GetTotalsByTypeAsync(drawer.Id);
            if (totals.Values.Sum() < amount)
            {
                throw new InvalidOperationException("The drawer does not hold that much cash.");
            }
        }

        var movement = new CashMovement
        {
            CashDrawerSessionId = drawer.Id,
            TheaterId = theaterId,
            Type = request.Type,
            Amount = request.Type == CashMovementType.PayOut ? -amount : amount,
            UserId = staffUserId,
            Note = note,
        };
        _uow.CashDrawerStore.StageMovement(movement);
        // Staged on the same unit of work, so the audit row and the movement are saved together.
        await _audit.LogAsync(new AuditEntry
        {
            TheaterId = theaterId,
            ActorUserId = staffUserId,
            ApproverUserId = approverUserId,
            Action = request.Type == CashMovementType.PayOut ? AuditAction.CashPayOut : AuditAction.Other,
            EntityType = nameof(CashDrawerSession),
            EntityId = drawer.Id,
            Amount = amount,
            Reason = note,
        });
        await _uow.SaveChangesAsync();

        return await ToDrawerDtoAsync(drawer);
    }

    private async Task<CashDrawerDTO> ToDrawerDtoAsync(CashDrawerSession session)
    {
        var totals = await _uow.CashDrawerStore.GetTotalsByTypeAsync(session.Id);
        var movements = await _uow.CashDrawerStore.GetRecentMovementsAsync(session.Id, _recentMovementCount);
        return new CashDrawerDTO
        {
            IsOpen = true,
            Id = session.Id,
            TheaterId = session.TheaterId,
            TerminalName = session.TerminalName,
            OpenedAt = session.OpenedAt,
            OpeningFloat = session.OpeningFloat,
            CashSales = TotalOf(totals, CashMovementType.Sale),
            PayIns = TotalOf(totals, CashMovementType.PayIn),
            PayOuts = -TotalOf(totals, CashMovementType.PayOut),
            Refunds = -TotalOf(totals, CashMovementType.Refund),
            ExpectedCash = totals.Values.Sum(),
            RecentMovements = movements.Select(m => new CashMovementDTO
            {
                Id = m.Id,
                Type = m.Type,
                Amount = m.Amount,
                InvoiceId = m.InvoiceId,
                Note = m.Note,
                CreationTime = m.CreationTime,
            }).ToList(),
        };
    }

    private static double TotalOf(Dictionary<CashMovementType, double> totals, CashMovementType type)
    {
        return totals.TryGetValue(type, out var total) ? total : 0;
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

    private static double Whole(double value)
    {
        return Math.Round(value, 0, MidpointRounding.AwayFromZero);
    }
}
