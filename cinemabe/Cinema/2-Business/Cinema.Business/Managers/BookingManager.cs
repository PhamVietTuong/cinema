using System.Collections.Concurrent;
using Cinema.Business.Contracts;
using Cinema.Business.Contracts.Payments;
using Cinema.Business.DTO;
using Cinema.Business.DTO.Booking;
using Cinema.Business.DTO.Invoices;
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

    private readonly IPaymentGatewayResolver _gateways;
    private readonly INotificationService _notifications;
    private readonly ISmsNotificationService _sms;
    private readonly ISeatNotificationService _seatNotifications;

    public BookingManager(IApplicationUnitOfWork uow, IPaymentGatewayResolver gateways, INotificationService notifications, ISmsNotificationService sms, ISeatNotificationService seatNotifications)
    {
        _uow = uow;
        _gateways = gateways;
        _notifications = notifications;
        _sms = sms;
        _seatNotifications = seatNotifications;
    }

    public async Task<DefaultSearchResults<SeatDTO>> GetSeatsAsync(PagingSearchDTO search)
    {
        var showTimeId = search.Filters.GetGuid("showTimeId") ?? Guid.Empty;
        var roomId     = search.Filters.GetGuid("roomId")     ?? Guid.Empty;

        var seats        = await _uow.SeatStore.GetByRoomAsync(roomId);
        var bookedIds    = (await _uow.SeatStore.GetBookedSeatIdsAsync(showTimeId, roomId)).ToHashSet();
        var showTimeRoom = await _uow.ShowTimeStore.GetShowTimeRoomAsync(showTimeId, roomId);
        var pricing      = await BuildSeatPricingContextAsync(showTimeRoom);

        // The client assigns each seat its own patron category locally (from ticket-quantity "slots")
        // and computes IsAllowedForPatronCategory itself from each category's allowedSeatTypeIds
        // (avoids a seat re-fetch on every quantity change) — this response always reports the
        // unfiltered/unrestricted default of true.
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
                Price         = PriceSeat(pricing, s.SeatTypeId, s.SeatType?.PriceMultiplier ?? 1),
                IsLocked      = isLocked && !isBooked,
                SeatGroupId   = s.SeatGroupId,
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
            var showTimeRoom = await _uow.ShowTimeStore.GetShowTimeRoomAsync(request.ShowTimeId, request.RoomId);
            if (showTimeRoom == null)
            {
                throw new InvalidOperationException("ShowTime/Room combination not found.");
            }
            var pricing = await BuildSeatPricingContextAsync(showTimeRoom);
            var patronCategories = await LoadPatronCategoriesAsync(request.RoomId, request.Seats);
            var allowedSeatTypesByCategory = await LoadAllowedSeatTypesAsync(patronCategories.Keys);

            var bookedIds = (await _uow.SeatStore.GetBookedSeatIdsAsync(request.ShowTimeId, request.RoomId)).ToHashSet();

            double ticketTotal = 0;
            var tickets    = new List<InvoiceTicket>();
            var ticketItems = new List<TicketItemDTO>();

            foreach (var seatItem in request.Seats)
            {
                if (bookedIds.Contains(seatItem.SeatId))
                {
                    throw new InvalidOperationException($"Seat {seatItem.SeatId} is already booked.");
                }

                // Reject a seat another user is actively holding (SignalR lock). Enforced only when the
                // client supplies its connection id, so the booker's own held seats still pass. IsSeatLocked
                // applies the 5-minute lock expiry and the owner (connection) exclusion.
                if (!string.IsNullOrEmpty(request.ConnectionId)
                    && IsSeatLocked(request.ShowTimeId, request.RoomId, seatItem.SeatId, request.ConnectionId))
                {
                    throw new InvalidOperationException($"Seat {seatItem.SeatId} is being held by another user.");
                }

                var seat = await _uow.SeatStore.GetByIdAsync(seatItem.SeatId);
                if (seat == null)
                {
                    throw new KeyNotFoundException($"Seat {seatItem.SeatId} not found.");
                }

                // Price is BasePrice scaled by the ticket-price matrix multiplier (theater/roomType/seatType/
                // timeSlot/holiday) when a row matches, falling back to BasePrice × the seat type's multiplier
                // × holiday factor otherwise. See BuildSeatPricingContextAsync.
                var seatType   = await _uow.SeatTypeStore.GetByIdAsync(seat.SeatTypeId);
                var multiplier = seatType?.PriceMultiplier ?? 1;
                var basePrice  = PriceSeat(pricing, seat.SeatTypeId, multiplier);

                // Self-reported patron category (Adult/Student/Senior/Child), checked visually at the
                // theater rather than verified here. A supplied id must resolve to an active category in
                // this room's theater, or the booking is rejected outright — silently falling back to
                // full price would surprise the customer, and silently discounting an unknown id would be
                // a revenue hole. The category reduces this ticket's own price (stacks with the
                // membership/promo discount ComputePricingAsync applies to the invoice total afterward).
                PatronCategory? category = null;
                if (seatItem.PatronCategoryId is Guid patronCategoryId && patronCategoryId != Guid.Empty)
                {
                    if (!patronCategories.TryGetValue(patronCategoryId, out category) || !category.IsActive)
                    {
                        throw new InvalidOperationException("Selected patron category is invalid or unavailable.");
                    }

                    // Server-side enforcement of the seat-type gate — the client's greyed-out seat map is
                    // cosmetic only. Empty allowed-set = category is unrestricted.
                    if (allowedSeatTypesByCategory.TryGetValue(patronCategoryId, out var allowedSeatTypeIds)
                        && allowedSeatTypeIds.Count > 0
                        && !allowedSeatTypeIds.Contains(seat.SeatTypeId))
                    {
                        throw new InvalidOperationException($"Seat {seatItem.SeatId} is not available for the selected patron category.");
                    }
                }
                var price = ApplyPatronDiscount(basePrice, category?.DiscountPercent ?? 0);
                ticketTotal += price;

                // Unguessable per-ticket token; encoded as the e-ticket QR and checked at the gate.
                var qr = Guid.NewGuid().ToString("N");
                tickets.Add(new InvoiceTicket
                {
                    ShowTimeId            = request.ShowTimeId,
                    RoomId                = request.RoomId,
                    SeatId                = seatItem.SeatId,
                    Price                 = price,
                    PatronCategoryId      = category?.Id,
                    PatronCategoryName    = category?.Name,
                    PatronDiscountPercent = category?.DiscountPercent ?? 0,
                    QrCode                = qr,
                });

                ticketItems.Add(new TicketItemDTO
                {
                    SeatLabel             = $"{seat.RowName}{seat.ColIndex}",
                    SeatType              = seatType?.Name ?? string.Empty,
                    Price                 = price,
                    PatronCategory        = category?.Name ?? string.Empty,
                    PatronDiscountPercent = category?.DiscountPercent ?? 0,
                    QrCode                = qr,
                });
            }

            double foodTotal = 0;
            var foods = new List<InvoiceFoodAndDrink>();
            foreach (var f in request.Foods)
            {
                var food = await _uow.FoodAndDrinkStore.GetByIdAsync(f.FoodAndDrinkId);
                if (food == null)
                {
                    throw new KeyNotFoundException($"Food item {f.FoodAndDrinkId} not found.");
                }
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
                await ComputePricingAsync(userId, total, request.DiscountCode, request.RoomId, request.ShowTimeId);

            // Loyalty redemption: spend points for a discount. The points are reserved (deducted) now and
            // restored if the booking is cancelled, expires, or is refunded. 1 point = _pointValueVnd VND,
            // capped at the customer's balance and the order total so the amount can't go negative.
            var pointsRedeemed = 0;
            if (request.PointsToRedeem > 0)
            {
                var redeemingUser = await _uow.UserStore.GetByIdAsync(userId);
                if (redeemingUser is not null)
                {
                    var maxByBalance = redeemingUser.Points;
                    var maxByAmount  = (int)(finalAmount / _pointValueVnd);
                    pointsRedeemed = Math.Min(request.PointsToRedeem, Math.Min(maxByBalance, maxByAmount));
                    if (pointsRedeemed > 0)
                    {
                        var pointsValue = pointsRedeemed * _pointValueVnd;
                        finalAmount    -= pointsValue;
                        discountAmount += pointsValue;
                        redeemingUser.Points -= pointsRedeemed;
                        await _uow.UserStore.UpdateAsync(redeemingUser);
                    }
                }
            }

            // Gift card: draw down its balance to cover part (or all) of the remaining amount. Reserved
            // now and restored if the booking is cancelled, expires, or is refunded. An invalid/expired
            // code provided by the customer is rejected (so they're never silently charged full price).
            Guid? giftCardId = null;
            double giftCardAmount = 0;
            if (!string.IsNullOrWhiteSpace(request.GiftCardCode))
            {
                var card = await _uow.GiftCardStore.GetByCodeAsync(request.GiftCardCode.Trim());
                var usable = card is not null && card.IsActive
                             && (card.ExpiresAt is null || card.ExpiresAt > DateTime.UtcNow);
                if (!usable)
                {
                    throw new InvalidOperationException("Invalid or expired gift card.");
                }
                giftCardAmount = Math.Min(card!.Balance, finalAmount);
                if (giftCardAmount > 0)
                {
                    finalAmount    -= giftCardAmount;
                    discountAmount += giftCardAmount;
                    card.Balance   -= giftCardAmount;
                    await _uow.GiftCardStore.UpdateAsync(card);
                    giftCardId = card.Id;
                }
            }

            var invoice = new Invoice
            {
                Code                = GenerateCode(),
                UserId              = userId,
                TotalAmount         = total,
                DiscountAmount      = discountAmount,
                FinalAmount         = finalAmount,
                PointsRedeemed      = pointsRedeemed,
                GiftCardId          = giftCardId,
                GiftCardAmount      = giftCardAmount,
                DiscountId          = discountId,
                Status              = InvoiceStatus.Pending,
                PaymentMethod       = request.PaymentMethod,
                InvoiceTickets      = tickets,
                InvoiceFoodAndDrinks = foods
            };

            await _uow.InvoiceStore.CreateAsync(invoice);
            await _uow.CommitTransactionAsync();

            // Clear the booker's own advisory locks on the seats just booked (SeatBooked below supersedes
            // them; emitting SeatUnlocked first would briefly flash the seat as available to other viewers)
            // and tell everyone else in the room these seats are now unavailable.
            if (!string.IsNullOrEmpty(request.ConnectionId))
            {
                foreach (var seatItem in request.Seats)
                {
                    UnlockSeat(request.ShowTimeId, request.RoomId, seatItem.SeatId, request.ConnectionId);
                }
            }
            await _seatNotifications.NotifySeatsBookedAsync(request.ShowTimeId, request.RoomId, request.Seats.Select(s => s.SeatId).ToList());

            return new BookingResultDTO
            {
                InvoiceId      = invoice.Id,
                InvoiceCode    = invoice.Code,
                TotalAmount    = total,
                DiscountAmount = discountAmount,
                FinalAmount    = finalAmount,
                PointsRedeemed = pointsRedeemed,
                Status         = InvoiceStatus.Pending,
                Tickets        = ticketItems
            };
        }
        catch (SeatUnavailableException)
        {
            // Another booking (possibly on another server instance) claimed a seat first — the DB unique
            // index rejected the insert. Surface it like the in-process "already booked" check.
            await _uow.RollbackTransactionAsync();
            throw new InvalidOperationException("One or more selected seats were just booked by someone else.");
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

    public async Task<PaymentInitiationDTO?> InitiatePaymentAsync(Guid userId, Guid invoiceId, string? provider, string? returnUrl)
    {
        var invoice = await _uow.InvoiceStore.GetByIdAsync(invoiceId);
        if (invoice == null)
        {
            return null;
        }
        // Object-level authorization: only the invoice owner may start its payment.
        if (invoice.UserId != userId)
        {
            return null;
        }
        // Only a Pending invoice can be paid.
        if (invoice.Status != InvoiceStatus.Pending)
        {
            return null;
        }

        // Fully covered (e.g. by a gift card) — nothing to charge, so finalize immediately with no gateway.
        if (invoice.FinalAmount <= 0)
        {
            await FinalizePaidInvoiceAsync(invoice, "GIFTCARD-FULL");
            return new PaymentInitiationDTO
            {
                Provider         = "None",
                PaymentReference = invoice.PaymentReference ?? "GIFTCARD-FULL",
                RedirectUrl      = null,
                AlreadyPaid      = true,
            };
        }

        var gateway = _gateways.Resolve(provider);
        var initiation = await gateway.CreatePaymentAsync(invoice.Id, invoice.FinalAmount, returnUrl);

        // Remember which provider owns this invoice so ConfirmPayment/HandlePaymentCallback resolve the same one.
        invoice.PaymentMethod    = gateway.Name;
        invoice.PaymentReference = initiation.PaymentReference;
        await _uow.InvoiceStore.UpdateAsync(invoice);
        await _uow.SaveChangesAsync();

        return new PaymentInitiationDTO
        {
            Provider         = gateway.Name,
            PaymentReference = initiation.PaymentReference,
            RedirectUrl      = initiation.RedirectUrl,
        };
    }

    public async Task<bool> ConfirmPaymentAsync(Guid userId, Guid invoiceId, string paymentReference)
    {
        var invoice = await _uow.InvoiceStore.GetByIdAsync(invoiceId);
        if (invoice == null)
        {
            return false;
        }
        // Object-level authorization: only the invoice owner may confirm its payment.
        if (invoice.UserId != userId)
        {
            return false;
        }
        // Only a Pending invoice can transition to Paid (prevents re-confirming / double-charge state churn).
        if (invoice.Status != InvoiceStatus.Pending)
        {
            return false;
        }
        // Fall back to the reference stored at initiation. The caller returning from a redirect
        // doesn't necessarily carry it, and the server already knows which one it issued.
        var reference = string.IsNullOrWhiteSpace(paymentReference)
            ? invoice.PaymentReference ?? string.Empty
            : paymentReference;

        // Verify with the invoice's provider (must succeed and the captured amount must match FinalAmount).
        // Only the dev Sandbox approves this synchronous path; real providers are callback-authoritative, so
        // this returns false for them and the invoice is instead finalized by HandlePaymentCallbackAsync.
        var verification = await _gateways.Resolve(invoice.PaymentMethod).VerifyPaymentAsync(reference, invoice.FinalAmount);
        if (!verification.Success)
        {
            return false;
        }

        await FinalizePaidInvoiceAsync(invoice, reference);
        return true;
    }

    public async Task<bool> HandlePaymentCallbackAsync(string provider, IReadOnlyDictionary<string, string> callbackData)
    {
        // Signature-verify the provider callback first; this is the authoritative "money moved" signal.
        var result = _gateways.Resolve(provider).ParseCallback(callbackData);
        if (!result.Success)
        {
            return false;
        }

        var invoice = await _uow.InvoiceStore.GetByIdAsync(result.InvoiceId);
        if (invoice == null)
        {
            return false;
        }
        // Idempotency: a provider may deliver the callback more than once.
        if (invoice.Status == InvoiceStatus.Paid)
        {
            return true;
        }
        if (invoice.Status != InvoiceStatus.Pending)
        {
            return false;
        }

        await FinalizePaidInvoiceAsync(invoice, result.PaymentReference);
        return true;
    }

    /// <summary>Marks the invoice Paid and applies the side effects: loyalty accrual, tier re-eval,
    /// promo-code consumption, and the confirmation notification. Caller must have verified payment.</summary>
    private async Task FinalizePaidInvoiceAsync(Invoice invoice, string paymentReference)
    {
        invoice.Status           = InvoiceStatus.Paid;
        invoice.PaymentReference = paymentReference;
        invoice.PaidAt           = DateTime.UtcNow;
        await _uow.InvoiceStore.UpdateAsync(invoice);

        // Loyalty: accrue points on the paid amount and re-evaluate the membership tier.
        var user = await _uow.UserStore.GetByIdAsync(invoice.UserId);
        if (user is not null)
        {
            user.Points += (int)(invoice.FinalAmount / _pointsPerUnit);
            var tiers = await _uow.MemberShipStore.FindAsync(m => m.MinPoints <= user.Points);
            var tier  = tiers.OrderByDescending(m => m.MinPoints).FirstOrDefault();
            if (tier is not null)
            {
                user.MemberShipId = tier.Id;
            }
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
        {
            await _notifications.SendAsync(
                user.Email,
                $"Booking confirmed — {invoice.Code}",
                $"Your payment was received. Booking code: {invoice.Code}. " +
                $"Total paid: {invoice.FinalAmount:0} VND. Show your e-ticket QR at the entrance.");

            // Also send an SMS confirmation when the user has a phone (dev-log unless Twilio is configured).
            if (!string.IsNullOrWhiteSpace(user.Phone))
            {
                await _sms.SendSmsAsync(user.Phone,
                    $"Cinema: booking {invoice.Code} confirmed. Total {invoice.FinalAmount:0} VND. Show your e-ticket QR at the entrance.");
            }
        }
    }

    // ── Seat pricing ────────────────────────────────────────────────────────────
    // A seat's price is always anchored on the showtime's own BasePrice (which reflects the movie/format
    // being screened) — nothing is allowed to replace it outright, only scale it. When a ticket-price
    // matrix row (theater × roomType × seatType × timeSlot × isHoliday) matches, its PriceMultiplier scales
    // BasePrice instead of SeatType.PriceMultiplier — the matrix row is already seat-type-scoped, so
    // applying both would double-count the seat premium. It's already holiday-scoped too, so the holiday
    // factor is also skipped in that branch; only the fallback (BasePrice × SeatType multiplier) applies
    // the holiday factor, since there the holiday-ness hasn't been priced in yet. The context is resolved
    // once per showtime and reused for every seat; all store lookups are null-guarded so the fallback
    // holds when nothing is configured. A 3D screening adds a flat per-ticket surcharge on top of
    // whichever branch produced the price — the room class sets the base, the dimension is charged
    // separately (an IMAX 3D ticket pays both).
    private sealed record SeatPricingContext(
        double BasePrice,
        IReadOnlyDictionary<Guid, double> MultiplierBySeatType,
        double HolidayFactor,
        double ThreeDSurcharge);

    private static double PriceSeat(SeatPricingContext ctx, Guid seatTypeId, double seatMultiplier)
    {
        if (ctx.MultiplierBySeatType.TryGetValue(seatTypeId, out var matrixMultiplier))
        {
            return (ctx.BasePrice * matrixMultiplier) + ctx.ThreeDSurcharge;
        }
        return (ctx.BasePrice * seatMultiplier * ctx.HolidayFactor) + ctx.ThreeDSurcharge;
    }

    /// <summary>Reduces a single ticket's price by its patron category's percent-off. This is a price
    /// input like the seat-type/holiday factors above — not an invoice discount line — so it is applied
    /// once, here, before the ticket total is summed; ComputePricingAsync's membership/promo discounts
    /// then apply to that already-adjusted total, so the two never double-count each other.</summary>
    private static double ApplyPatronDiscount(double price, double discountPercent)
    {
        var pct = Math.Clamp(discountPercent, 0, 100);
        return Math.Round(Math.Max(0, price * (1 - pct / 100.0)), 2);
    }

    /// <summary>Resolves the active patron categories referenced by a booking's seats, scoped to the
    /// room's theater (categories are per-theater). Returns an empty dictionary — no query — when no
    /// seat requests one.</summary>
    private async Task<Dictionary<Guid, PatronCategory>> LoadPatronCategoriesAsync(Guid roomId, IEnumerable<BookingSeatItem> seats)
    {
        var ids = seats
            .Select(s => s.PatronCategoryId)
            .Where(id => id is Guid g && g != Guid.Empty)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, PatronCategory>();
        }

        var room = await _uow.RoomStore.GetByIdAsync(roomId);
        if (room is null)
        {
            return new Dictionary<Guid, PatronCategory>();
        }

        var categories = await _uow.PatronCategoryStore.FindAsync(c => c.TheaterId == room.TheaterId && ids.Contains(c.Id));
        return (categories ?? Enumerable.Empty<PatronCategory>()).ToDictionary(c => c.Id);
    }

    /// <summary>Batches the seat-type gate for the given patron category ids into one query. A category
    /// absent from the result, or mapped to an empty set, is unrestricted.</summary>
    private async Task<Dictionary<Guid, HashSet<Guid>>> LoadAllowedSeatTypesAsync(IEnumerable<Guid> patronCategoryIds)
    {
        var ids = patronCategoryIds.ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, HashSet<Guid>>();
        }

        var rows = await _uow.PatronCategorySeatTypeStore.FindByPatronCategoriesAsync(ids);
        return rows
            .GroupBy(r => r.PatronCategoryId)
            .ToDictionary(g => g.Key, g => g.Select(r => r.SeatTypeId).ToHashSet());
    }

    private async Task<SeatPricingContext> BuildSeatPricingContextAsync(ShowTimeRoom? showTimeRoom)
    {
        var basePrice = showTimeRoom?.BasePrice ?? 0;
        var empty = new Dictionary<Guid, double>();
        if (showTimeRoom is null)
        {
            return new SeatPricingContext(basePrice, empty, 1.0, 0);
        }

        var room     = await _uow.RoomStore.GetByIdAsync(showTimeRoom.RoomId);
        var showTime = await _uow.ShowTimeStore.GetByIdAsync(showTimeRoom.ShowTimeId);
        if (room is null || showTime is null)
        {
            return new SeatPricingContext(basePrice, empty, 1.0, 0);
        }

        // Only a 3D screening pays the surcharge, so only a 3D screening costs the extra lookup.
        var threeDSurcharge = 0.0;
        if (showTime.ProjectionForm == ProjectionForm.ThreeD)
        {
            var roomType = await _uow.RoomTypeStore.GetByIdAsync(room.RoomTypeId);
            threeDSurcharge = roomType?.ThreeDSurcharge ?? 0;
        }

        var date      = DateOnly.FromDateTime(showTime.StartTime);
        var timeOfDay = TimeOnly.FromDateTime(showTime.StartTime);

        // Holiday: any Holiday whose date matches the showtime's date scales the fallback price.
        var holidays  = await _uow.HolidayStore.FindAsync(h => h.Date == date) ?? Enumerable.Empty<Holiday>();
        var holiday   = holidays.FirstOrDefault();
        var isHoliday = holiday is not null;
        var holidayFactor = isHoliday ? holiday!.PriceMultiplier : 1.0;

        // The theater time slot whose [start, end) window contains the showtime's time-of-day.
        var slots = await _uow.TimeSlotStore.FindAsync(t => t.TheaterId == room.TheaterId) ?? Enumerable.Empty<TimeSlot>();
        var slot  = slots.FirstOrDefault(s => TimeInSlot(timeOfDay, s));

        var multipliers = new Dictionary<Guid, double>();
        if (slot is not null)
        {
            var rows = await _uow.TicketPriceStore.FindAsync(tp =>
                           tp.TheaterId == room.TheaterId
                           && tp.RoomTypeId == room.RoomTypeId
                           && tp.TimeSlotId == slot.Id
                           && tp.IsHoliday == isHoliday)
                       ?? Enumerable.Empty<TicketPrice>();
            foreach (var r in rows)
            {
                multipliers[r.SeatTypeId] = r.PriceMultiplier;
            }
        }

        return new SeatPricingContext(basePrice, multipliers, holidayFactor, threeDSurcharge);
    }

    private static bool TimeInSlot(TimeOnly t, TimeSlot slot)
    {
        if (!TimeOnly.TryParse(slot.StartTime, out var start) || !TimeOnly.TryParse(slot.EndTime, out var end))
        {
            return false;
        }
        // Only match a normal, non-wrapping [start, end) window.
        return end > start && t >= start && t < end;
    }

    public async Task<TicketValidationDTO> ValidateTicketAsync(string qrCode)
    {
        var ticket = await _uow.InvoiceStore.GetTicketByQrAsync(qrCode);
        if (ticket == null)
        {
            throw new KeyNotFoundException("Ticket not found.");
        }
        if (ticket.Invoice.Status != InvoiceStatus.Paid)
        {
            throw new InvalidOperationException("Ticket has not been paid.");
        }
        if (ticket.IsUsed)
        {
            throw new InvalidOperationException("Ticket has already been used.");
        }

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
            PatronCategory = ticket.PatronCategoryName ?? string.Empty,
            Message     = "Ticket valid — checked in."
        };
    }

    // 1 loyalty point per 10,000 VND of the paid amount.
    private const int _pointsPerUnit = 10000;
    // Redemption value: 1 loyalty point is worth this many VND when spent at checkout.
    private const int _pointValueVnd = 1000;

    // Pricing: apply the member's tier discount first, then either a valid promo code or — when no
    // code is given — the best-value auto-apply promotion whose scope (theater/movie/day/time) matches
    // this booking. A provided-but-invalid code is rejected so the customer is never silently charged
    // full price. Returns (discountAmount, finalAmount, discountId).
    private async Task<(double DiscountAmount, double FinalAmount, Guid? DiscountId)> ComputePricingAsync(
        Guid userId, double total, string? discountCode, Guid roomId, Guid showTimeId)
    {
        var running = total;

        var user = await _uow.UserStore.GetByIdAsync(userId);
        if (user?.MemberShipId is Guid membershipId)
        {
            var membership = await _uow.MemberShipStore.GetByIdAsync(membershipId);
            if (membership is { DiscountPercent: > 0 })
            {
                running -= running * (membership.DiscountPercent / 100.0);
            }
        }

        var now = DateTime.UtcNow;
        var bookingTheaterId = (await _uow.RoomStore.GetByIdAsync(roomId))?.TheaterId;
        var showTime = await _uow.ShowTimeStore.GetByIdAsync(showTimeId);

        Guid? discountId = null;
        if (!string.IsNullOrWhiteSpace(discountCode))
        {
            var code = discountCode.Trim();
            var discount = await _uow.DiscountStore.GetByCodeAsync(code);
            if (discount is null
                || !discount.IsActive
                || discount.StartDate > now || now > discount.EndDate
                || (discount.MaxUsage != null && discount.UsedCount >= discount.MaxUsage)
                || !MatchesScope(discount, bookingTheaterId, showTime))
                throw new InvalidOperationException("Discount code is invalid or no longer available.");

            running -= ApplyPercent(running, discount);
            discountId = discount.Id;
        }
        else
        {
            // Auto-apply the best-value promotion whose scope matches this booking (no code needed).
            var candidates = await _uow.DiscountStore.GetActiveAutoApplyAsync(now);
            var best = candidates
                .Where(d => (d.MaxUsage == null || d.UsedCount < d.MaxUsage)
                            && MatchesScope(d, bookingTheaterId, showTime))
                .Select(d => (Discount: d, Amount: ApplyPercent(running, d)))
                .OrderByDescending(x => x.Amount)
                .FirstOrDefault();
            if (best.Discount != null && best.Amount > 0)
            {
                running -= best.Amount;
                discountId = best.Discount.Id;
            }
        }

        if (running < 0)
        {
            running = 0;
        }
        return (Math.Round(total - running, 2), Math.Round(running, 2), discountId);
    }

    // Percentage reduction on the running total, capped by the promotion's MaxDiscountAmount.
    private static double ApplyPercent(double running, Discount d)
    {
        var amount = running * (d.Percent / 100.0);
        if (d.MaxDiscountAmount is double cap && amount > cap)
        {
            amount = cap;
        }
        return amount;
    }

    // Checks a promotion's theater / movie / day-of-week / time-of-day scope against the booking.
    private static bool MatchesScope(Discount d, Guid? bookingTheaterId, ShowTime? showTime)
    {
        if (!d.ApplyToAllTheaters)
        {
            if (bookingTheaterId == null || d.DiscountTheaters.All(t => t.TheaterId != bookingTheaterId))
            {
                return false;
            }
        }

        var hasShowScope = d.MovieId != null || d.DaysOfWeekMask != null
                           || d.StartTimeOfDay != null || d.EndTimeOfDay != null;
        if (hasShowScope && showTime == null)
        {
            // cannot verify a movie/day/time-scoped promotion without the showtime
            return false;
        }

        if (showTime != null)
        {
            if (d.MovieId != null && showTime.MovieId != d.MovieId)
            {
                return false;
            }
            if (d.DaysOfWeekMask is int mask && (mask & (1 << (int)showTime.StartTime.DayOfWeek)) == 0)
            {
                return false;
            }
            var start = TimeOnly.FromDateTime(showTime.StartTime);
            if (d.StartTimeOfDay is TimeOnly from && start < from)
            {
                return false;
            }
            if (d.EndTimeOfDay is TimeOnly to && start > to)
            {
                return false;
            }
        }
        return true;
    }

    public async Task<bool> CancelBookingAsync(Guid userId, Guid invoiceId)
    {
        var invoice = await _uow.InvoiceStore.GetByIdAsync(invoiceId);
        if (invoice == null || invoice.UserId != userId)
        {
            return false;
        }
        if (invoice.Status != InvoiceStatus.Pending)
        {
            return false;
        }
        invoice.Status = InvoiceStatus.Cancelled;
        await _uow.InvoiceStore.UpdateAsync(invoice);
        await _uow.InvoiceStore.DeactivateTicketsAsync(invoice.Id);
        await RestoreRedeemedPointsAsync(invoice);
        await RestoreGiftCardAsync(invoice);
        await _uow.SaveChangesAsync();
        return true;
    }

    // Returns the loyalty points reserved for a booking that did not complete (cancelled/expired/refunded).
    private async Task RestoreRedeemedPointsAsync(Invoice invoice)
    {
        if (invoice.PointsRedeemed <= 0)
        {
            return;
        }
        var user = invoice.User ?? await _uow.UserStore.GetByIdAsync(invoice.UserId);
        if (user is not null)
        {
            user.Points += invoice.PointsRedeemed;
            await _uow.UserStore.UpdateAsync(user);
        }
    }

    // Returns the gift-card balance drawn for a booking that did not complete (cancelled/expired/refunded).
    private async Task RestoreGiftCardAsync(Invoice invoice)
    {
        if (invoice.GiftCardId is not Guid giftCardId || invoice.GiftCardAmount <= 0)
        {
            return;
        }
        var card = await _uow.GiftCardStore.GetByIdAsync(giftCardId);
        if (card is not null)
        {
            card.Balance += invoice.GiftCardAmount;
            await _uow.GiftCardStore.UpdateAsync(card);
        }
    }

    public async Task<bool> RefundBookingAsync(Guid userId, Guid invoiceId, bool isAdmin)
    {
        var invoice = await _uow.InvoiceStore.GetWithDetailsAsync(invoiceId);
        if (invoice == null)
        {
            return false;
        }
        // Object-level authorization: the owner, or an admin, may refund.
        if (!isAdmin && invoice.UserId != userId)
        {
            return false;
        }
        // Only a Paid invoice can be refunded (Pending is cancelled, not refunded).
        if (invoice.Status != InvoiceStatus.Paid)
        {
            return false;
        }
        // Don't refund a ticket that was already checked in at the gate.
        if (invoice.InvoiceTickets.Any(t => t.IsUsed))
        {
            return false;
        }
        // Don't refund once the (earliest) showtime has started.
        var earliestStart = invoice.InvoiceTickets
            .Select(t => t.ShowTimeRoom?.ShowTime?.StartTime)
            .Where(s => s.HasValue)
            .DefaultIfEmpty(null)
            .Min();
        if (earliestStart.HasValue && earliestStart.Value <= DateTime.Now)
        {
            return false;
        }

        // Return the money. Stripe/Sandbox process via API; VNPay/MoMo are refunded out-of-band via the
        // merchant portal, so an admin is allowed to record the refund even when the API declines it.
        var refund = await _gateways.Resolve(invoice.PaymentMethod).RefundAsync(invoice.PaymentReference ?? "", invoice.FinalAmount);
        if (!refund.Success && !isAdmin)
        {
            return false;
        }

        // Mark refunded. Because seat occupancy counts only Pending/Paid invoices, this frees the seats.
        invoice.Status     = InvoiceStatus.Refunded;
        invoice.RefundedAt = DateTime.UtcNow;
        await _uow.InvoiceStore.UpdateAsync(invoice);
        await _uow.InvoiceStore.DeactivateTicketsAsync(invoice.Id);

        // Reverse the loyalty points accrued at payment, give back any points spent on this booking,
        // and re-evaluate the membership tier.
        var user = invoice.User ?? await _uow.UserStore.GetByIdAsync(invoice.UserId);
        if (user is not null)
        {
            user.Points -= (int)(invoice.FinalAmount / _pointsPerUnit);
            user.Points += invoice.PointsRedeemed;
            if (user.Points < 0)
            {
                user.Points = 0;
            }
            var tiers = await _uow.MemberShipStore.FindAsync(m => m.MinPoints <= user.Points);
            var tier  = tiers.OrderByDescending(m => m.MinPoints).FirstOrDefault();
            user.MemberShipId = tier?.Id;
            await _uow.UserStore.UpdateAsync(user);
        }

        // Give the promo code's usage back.
        if (invoice.DiscountId is Guid usedDiscountId)
        {
            var discount = invoice.Discount ?? await _uow.DiscountStore.GetByIdAsync(usedDiscountId);
            if (discount is not null && discount.UsedCount > 0)
            {
                discount.UsedCount -= 1;
                await _uow.DiscountStore.UpdateAsync(discount);
            }
        }

        // Give the gift-card balance back.
        await RestoreGiftCardAsync(invoice);

        await _uow.SaveChangesAsync();

        if (user is not null)
        {
            await _notifications.SendAsync(
                user.Email,
                $"Refund processed — {invoice.Code}",
                $"Your booking {invoice.Code} has been refunded. Amount: {invoice.FinalAmount:0} VND.");
        }

        return true;
    }

    public async Task<int> ExpireStalePendingBookingsAsync(TimeSpan age)
    {
        var cutoff = DateTime.UtcNow - age;
        var stale  = await _uow.InvoiceStore.GetStalePendingAsync(cutoff);
        if (stale.Count == 0)
        {
            return 0;
        }
        foreach (var invoice in stale)
        {
            // Cancelling frees the held seats — GetBookedSeatIdsAsync only counts Pending/Paid.
            invoice.Status = InvoiceStatus.Cancelled;
            await _uow.InvoiceStore.UpdateAsync(invoice);
            await _uow.InvoiceStore.DeactivateTicketsAsync(invoice.Id);
            await RestoreRedeemedPointsAsync(invoice);
            await RestoreGiftCardAsync(invoice);
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
        {
            _lockedSeats.TryRemove(key, out _);
        }
    }

    public bool IsSeatLocked(Guid showTimeId, Guid roomId, Guid seatId, string? excludeConnectionId = null)
    {
        var key = SeatKey(showTimeId, roomId, seatId);
        if (!_lockedSeats.TryGetValue(key, out var info))
        {
            return false;
        }
        if (excludeConnectionId != null && info.ConnectionId == excludeConnectionId)
        {
            return false;
        }
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
            if (entry.Value.ConnectionId != connectionId)
            {
                continue;
            }
            if (_lockedSeats.TryRemove(entry.Key, out _) && TryParseSeatKey(entry.Key, out var ids))
            {
                released.Add(ids);
            }
        }
        return released;
    }

    private static string SeatKey(Guid showTimeId, Guid roomId, Guid seatId)
    {
        return $"{showTimeId}:{roomId}:{seatId}";
    }

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
    private static string GenerateCode()
    {
        return $"CIN{DateTime.UtcNow:yyyyMMddHHmmss}{Random.Shared.Next(1000, 9999)}";
    }
}
