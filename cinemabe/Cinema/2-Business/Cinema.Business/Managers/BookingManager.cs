using System.Collections.Concurrent;
using Cinema.Business.Contracts;
using Cinema.Business.Contracts.Payments;
using Cinema.Business.DTO;
using Cinema.Business.DTO.Booking;
using Cinema.Business.DTO.Requests;
using Cinema.Business.Extensions;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Cinema.Data.Enums;

namespace Cinema.Business.Managers;

public class BookingManager : IBookingManager
{
    private readonly IApplicationUnitOfWork _uow;
    // connectionId -> (connectionId, lockedAt)
    private static readonly ConcurrentDictionary<string, (string ConnectionId, DateTime LockedAt)> _lockedSeats = new();
    // Process-local booking gate keyed by {showTimeId}:{roomId}. Serializes the read-booked-then-insert
    // sequence so two concurrent requests can't both pass the "seat free" check and double-sell a seat.
    // Consistent with the process-local lock design above; a multi-instance deployment must additionally
    // enforce this at the DB (e.g. a unique constraint on active (ShowTimeId, RoomId, SeatId)).
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _bookingGates = new();
    private static SemaphoreSlim BookingGate(Guid showTimeId, Guid roomId)
        => _bookingGates.GetOrAdd($"{showTimeId}:{roomId}", _ => new SemaphoreSlim(1, 1));

    private readonly IPaymentGateway _paymentGateway;
    private readonly INotificationService _notifications;

    public BookingManager(IApplicationUnitOfWork uow, IPaymentGateway paymentGateway, INotificationService notifications)
    {
        _uow = uow;
        _paymentGateway = paymentGateway;
        _notifications = notifications;
    }

    public async Task<DefaultSearchResults<SeatDTO>> GetSeatsAsync(PagingSearchDTO search)
    {
        var showTimeId = search.Filters.GetGuid("showTimeId") ?? Guid.Empty;
        var roomId     = search.Filters.GetGuid("roomId")     ?? Guid.Empty;

        var seats        = await _uow.SeatStore.GetByRoomAsync(roomId);
        var bookedIds    = (await _uow.SeatStore.GetBookedSeatIdsAsync(showTimeId, roomId)).ToHashSet();
        var showTimeRoom = await _uow.ShowTimeStore.GetShowTimeRoomAsync(showTimeId, roomId);

        var dtos = seats.Select(s =>
        {
            var key      = SeatKey(showTimeId, roomId, s.Id);
            var isBooked = bookedIds.Contains(s.Id);
            var isLocked = _lockedSeats.ContainsKey(key);
            return new SeatDTO
            {
                Id            = s.Id,
                RowName       = s.RowName,
                ColIndex      = s.ColIndex,
                SeatTypeId    = s.SeatTypeId,
                SeatTypeName  = s.SeatType?.Name ?? "",
                SeatTypeColor = s.SeatType?.Color ?? "#808080",
                Status        = isBooked ? SeatStatus.Occupied : isLocked ? SeatStatus.Reserved : SeatStatus.Available,
                Price         = (showTimeRoom?.BasePrice ?? 0) * (s.SeatType?.PriceMultiplier ?? 1),
                IsLocked      = isLocked && !isBooked,
                SeatGroupId   = s.SeatGroupId
            };
        }).ToList();

        return new DefaultSearchResults<SeatDTO>
        {
            Results      = dtos,
            TotalCount   = dtos.Count,
            CountPerPage = dtos.Count,
            Page         = 1
        };
    }

    public async Task<BookingResultDTO> CreateBookingAsync(Guid userId, CreateBookingRequest request)
    {
        // Serialize concurrent bookings for the same showtime+room so the "seat already booked" check
        // and the ticket insert happen atomically (prevents the check-then-insert double-booking race).
        var gate = BookingGate(request.ShowTimeId, request.RoomId);
        await gate.WaitAsync();
        await _uow.BeginTransactionAsync();
        try
        {
            var showTimeRoom = await _uow.ShowTimeStore.GetShowTimeRoomAsync(request.ShowTimeId, request.RoomId)
                               ?? throw new InvalidOperationException("ShowTime/Room combination not found.");

            var bookedIds = (await _uow.SeatStore.GetBookedSeatIdsAsync(request.ShowTimeId, request.RoomId)).ToHashSet();

            double ticketTotal = 0;
            var tickets    = new List<InvoiceTicket>();
            var ticketItems = new List<TicketItemDTO>();

            foreach (var seatItem in request.Seats)
            {
                if (bookedIds.Contains(seatItem.SeatId))
                    throw new InvalidOperationException($"Seat {seatItem.SeatId} is already booked.");

                var seat = await _uow.SeatStore.GetByIdAsync(seatItem.SeatId)
                           ?? throw new KeyNotFoundException($"Seat {seatItem.SeatId} not found.");

                // Price = showtime base price scaled by the seat type's multiplier.
                var seatType   = await _uow.SeatTypeStore.GetByIdAsync(seat.SeatTypeId);
                var multiplier = seatType?.PriceMultiplier ?? 1;
                var price      = showTimeRoom.BasePrice * multiplier;
                ticketTotal += price;

                tickets.Add(new InvoiceTicket
                {
                    ShowTimeId   = request.ShowTimeId,
                    RoomId       = request.RoomId,
                    SeatId       = seatItem.SeatId,
                    Price        = price,
                    // Unguessable per-ticket token; encoded as the e-ticket QR and checked at the gate.
                    QrCode       = Guid.NewGuid().ToString("N"),
                });

                ticketItems.Add(new TicketItemDTO
                {
                    SeatLabel = $"{seat.RowName}{seat.ColIndex}",
                    SeatType  = seatType?.Name ?? string.Empty,
                    Price     = price
                });
            }

            double foodTotal = 0;
            var foods = new List<InvoiceFoodAndDrink>();
            foreach (var f in request.Foods)
            {
                var food = await _uow.FoodAndDrinkStore.GetByIdAsync(f.FoodAndDrinkId)
                           ?? throw new KeyNotFoundException($"Food item {f.FoodAndDrinkId} not found.");
                foods.Add(new InvoiceFoodAndDrink
                {
                    FoodAndDrinkId = f.FoodAndDrinkId,
                    Quantity       = f.Quantity,
                    UnitPrice      = food.Price,
                    TotalPrice     = food.Price * f.Quantity
                });
                foodTotal += food.Price * f.Quantity;
            }

            var total = ticketTotal + foodTotal;
            var (discountAmount, finalAmount, discountId) =
                await ComputePricingAsync(userId, total, request.DiscountCode, request.RoomId);

            var invoice = new Invoice
            {
                Code                = GenerateCode(),
                UserId              = userId,
                TotalAmount         = total,
                DiscountAmount      = discountAmount,
                FinalAmount         = finalAmount,
                DiscountId          = discountId,
                Status              = InvoiceStatus.Pending,
                PaymentMethod       = request.PaymentMethod,
                InvoiceTickets      = tickets,
                InvoiceFoodAndDrinks = foods
            };

            await _uow.InvoiceStore.CreateAsync(invoice);
            await _uow.CommitTransactionAsync();

            return new BookingResultDTO
            {
                InvoiceId      = invoice.Id,
                InvoiceCode    = invoice.Code,
                TotalAmount    = total,
                DiscountAmount = discountAmount,
                FinalAmount    = finalAmount,
                Status         = InvoiceStatus.Pending,
                Tickets        = ticketItems
            };
        }
        catch
        {
            await _uow.RollbackTransactionAsync();
            throw;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<bool> ConfirmPaymentAsync(Guid userId, Guid invoiceId, string paymentReference)
    {
        var invoice = await _uow.InvoiceStore.GetByIdAsync(invoiceId);
        if (invoice == null) return false;
        // Object-level authorization: only the invoice owner may confirm its payment.
        if (invoice.UserId != userId) return false;
        // Only a Pending invoice can transition to Paid (prevents re-confirming / double-charge state churn).
        if (invoice.Status != InvoiceStatus.Pending) return false;
        // Verify the payment with the gateway (must succeed and the captured amount must match FinalAmount)
        // before marking Paid. The sandbox approves dev flows; a real provider plugs in behind IPaymentGateway.
        var verification = await _paymentGateway.VerifyPaymentAsync(paymentReference, invoice.FinalAmount);
        if (!verification.Success) return false;

        invoice.Status           = InvoiceStatus.Paid;
        invoice.PaymentReference = paymentReference;
        invoice.PaidAt           = DateTime.UtcNow;
        await _uow.InvoiceStore.UpdateAsync(invoice);

        // Loyalty: accrue points on the paid amount and re-evaluate the membership tier.
        var user = await _uow.UserStore.GetByIdAsync(userId);
        if (user is not null)
        {
            user.Points += (int)(invoice.FinalAmount / _pointsPerUnit);
            var tiers = await _uow.MemberShipStore.FindAsync(m => m.MinPoints <= user.Points);
            var tier  = tiers.OrderByDescending(m => m.MinPoints).FirstOrDefault();
            if (tier is not null) user.MemberShipId = tier.Id;
            await _uow.UserStore.UpdateAsync(user);
        }

        // Mark the promo code (if any) as consumed.
        if (invoice.DiscountId is Guid usedDiscountId)
        {
            var discount = await _uow.DiscountStore.GetByIdAsync(usedDiscountId);
            if (discount is not null)
            {
                discount.UsedCount += 1;
                await _uow.DiscountStore.UpdateAsync(discount);
            }
        }

        await _uow.SaveChangesAsync();

        // Booking confirmation (e-ticket). Dev sender logs it; a real sender emails/SMSes it.
        if (user is not null)
            await _notifications.SendAsync(
                user.Email,
                $"Booking confirmed — {invoice.Code}",
                $"Your payment was received. Booking code: {invoice.Code}. " +
                $"Total paid: {invoice.FinalAmount:0} VND. Show your e-ticket QR at the entrance.");

        return true;
    }

    public async Task<TicketValidationDTO> ValidateTicketAsync(string qrCode)
    {
        var ticket = await _uow.InvoiceStore.GetTicketByQrAsync(qrCode)
                     ?? throw new KeyNotFoundException("Ticket not found.");
        if (ticket.Invoice.Status != InvoiceStatus.Paid)
            throw new InvalidOperationException("Ticket has not been paid.");
        if (ticket.IsUsed)
            throw new InvalidOperationException("Ticket has already been used.");

        ticket.IsUsed = true;
        await _uow.SaveChangesAsync();

        return new TicketValidationDTO
        {
            Valid       = true,
            InvoiceCode = ticket.Invoice.Code,
            SeatLabel   = ticket.Seat != null ? $"{ticket.Seat.RowName}{ticket.Seat.ColIndex}" : string.Empty,
            MovieTitle  = ticket.ShowTimeRoom?.ShowTime?.Movie?.Title ?? string.Empty,
            RoomName    = ticket.ShowTimeRoom?.Room?.Name ?? string.Empty,
            ShowTime    = ticket.ShowTimeRoom?.ShowTime?.StartTime ?? default,
            Message     = "Ticket valid — checked in."
        };
    }

    // 1 loyalty point per 10,000 VND of the paid amount.
    private const int _pointsPerUnit = 10000;

    // Pricing: apply the member's tier discount first, then a valid promo code on the remainder
    // (capped by the code's MaxDiscountAmount). A provided-but-invalid code is rejected so the
    // customer is never silently charged full price. Returns (discountAmount, finalAmount, discountId).
    private async Task<(double DiscountAmount, double FinalAmount, Guid? DiscountId)> ComputePricingAsync(
        Guid userId, double total, string? discountCode, Guid roomId)
    {
        var running = total;

        var user = await _uow.UserStore.GetByIdAsync(userId);
        if (user?.MemberShipId is Guid membershipId)
        {
            var membership = await _uow.MemberShipStore.GetByIdAsync(membershipId);
            if (membership is { DiscountPercent: > 0 })
                running -= running * (membership.DiscountPercent / 100.0);
        }

        Guid? discountId = null;
        if (!string.IsNullOrWhiteSpace(discountCode))
        {
            var now  = DateTime.UtcNow;
            var code = discountCode.Trim();
            var discount = await _uow.DiscountStore.FindSingleAsync(d => d.Code == code);
            // A theater-scoped code only applies to bookings at that theater; null = system-wide.
            var bookingTheaterId = (await _uow.RoomStore.GetByIdAsync(roomId))?.TheaterId;
            if (discount is null
                || !discount.IsActive
                || discount.StartDate > now || now > discount.EndDate
                || (discount.MaxUsage != null && discount.UsedCount >= discount.MaxUsage)
                || (discount.TheaterId != null && discount.TheaterId != bookingTheaterId))
                throw new InvalidOperationException("Discount code is invalid or no longer available.");

            var promo = running * (discount.Percent / 100.0);
            if (discount.MaxDiscountAmount is double cap && promo > cap) promo = cap;
            running -= promo;
            discountId = discount.Id;
        }

        if (running < 0) running = 0;
        return (Math.Round(total - running, 2), Math.Round(running, 2), discountId);
    }

    public async Task<bool> CancelBookingAsync(Guid userId, Guid invoiceId)
    {
        var invoice = await _uow.InvoiceStore.GetByIdAsync(invoiceId);
        if (invoice == null || invoice.UserId != userId) return false;
        if (invoice.Status != InvoiceStatus.Pending) return false;
        invoice.Status = InvoiceStatus.Cancelled;
        await _uow.InvoiceStore.UpdateAsync(invoice);
        await _uow.SaveChangesAsync();
        return true;
    }

    public async Task<int> ExpireStalePendingBookingsAsync(TimeSpan age)
    {
        var cutoff = DateTime.UtcNow - age;
        var stale  = await _uow.InvoiceStore.GetStalePendingAsync(cutoff);
        if (stale.Count == 0) return 0;
        foreach (var invoice in stale)
        {
            // Cancelling frees the held seats — GetBookedSeatIdsAsync only counts Pending/Paid.
            invoice.Status = InvoiceStatus.Cancelled;
            await _uow.InvoiceStore.UpdateAsync(invoice);
        }
        await _uow.SaveChangesAsync();
        return stale.Count;
    }

    public void LockSeat(Guid showTimeId, Guid roomId, Guid seatId, string connectionId)
        => _lockedSeats[SeatKey(showTimeId, roomId, seatId)] = (connectionId, DateTime.UtcNow);

    public void UnlockSeat(Guid showTimeId, Guid roomId, Guid seatId, string connectionId)
    {
        var key = SeatKey(showTimeId, roomId, seatId);
        if (_lockedSeats.TryGetValue(key, out var info) && info.ConnectionId == connectionId)
            _lockedSeats.TryRemove(key, out _);
    }

    public bool IsSeatLocked(Guid showTimeId, Guid roomId, Guid seatId, string? excludeConnectionId = null)
    {
        var key = SeatKey(showTimeId, roomId, seatId);
        if (!_lockedSeats.TryGetValue(key, out var info)) return false;
        if (excludeConnectionId != null && info.ConnectionId == excludeConnectionId) return false;
        if (DateTime.UtcNow - info.LockedAt > TimeSpan.FromMinutes(5))
        {
            _lockedSeats.TryRemove(key, out _);
            return false;
        }
        return true;
    }

    public IReadOnlyList<(Guid ShowTimeId, Guid RoomId, Guid SeatId)> ReleaseConnectionLocks(string connectionId)
    {
        var released = new List<(Guid ShowTimeId, Guid RoomId, Guid SeatId)>();
        foreach (var entry in _lockedSeats)
        {
            if (entry.Value.ConnectionId != connectionId) continue;
            if (_lockedSeats.TryRemove(entry.Key, out _) && TryParseSeatKey(entry.Key, out var ids))
                released.Add(ids);
        }
        return released;
    }

    private static string SeatKey(Guid showTimeId, Guid roomId, Guid seatId) => $"{showTimeId}:{roomId}:{seatId}";

    private static bool TryParseSeatKey(string key, out (Guid ShowTimeId, Guid RoomId, Guid SeatId) ids)
    {
        ids = default;
        var parts = key.Split(':');
        if (parts.Length == 3
            && Guid.TryParse(parts[0], out var showTimeId)
            && Guid.TryParse(parts[1], out var roomId)
            && Guid.TryParse(parts[2], out var seatId))
        {
            ids = (showTimeId, roomId, seatId);
            return true;
        }
        return false;
    }
    private static string GenerateCode() => $"CIN{DateTime.UtcNow:yyyyMMddHHmmss}{Random.Shared.Next(1000, 9999)}";
}
