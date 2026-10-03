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
            var kind     = s.SeatGroupId.HasValue ? SeatKind.Double : SeatKind.Standard;
            return new SeatDTO
            {
                Id          = s.Id,
                RowName     = s.RowName,
                ColIndex    = s.ColIndex,
                IsDouble    = kind == SeatKind.Double,
                Status      = isBooked ? SeatStatus.Occupied : isLocked ? SeatStatus.Reserved : SeatStatus.Available,
                Price       = pricing.MinPriceByKind.TryGetValue(kind, out var fromPrice) ? fromPrice : 0,
                IsLocked    = isLocked && !isBooked,
                SeatGroupId = s.SeatGroupId,
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

    public async Task<List<ShowTimePriceDTO>> GetShowTimePricesAsync(Guid showTimeId, Guid roomId)
    {
        var showTimeRoom = await _uow.ShowTimeStore.GetShowTimeRoomAsync(showTimeId, roomId);
        var pricing = await BuildSeatPricingContextAsync(showTimeRoom);
        return pricing.ByCategoryId.Values
            .Select(c => new ShowTimePriceDTO
            {
                PatronCategoryId   = c.Id,
                PatronCategoryName = c.Name,
                SeatTypeId         = c.SeatTypeId,
                Kind               = c.Kind,
                IsDouble           = c.Kind == SeatKind.Double,
                Price              = c.Price,
            })
            .OrderBy(p => p.Price)
            .ToList();
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

            var bookedIds = (await _uow.SeatStore.GetBookedSeatIdsAsync(request.ShowTimeId, request.RoomId)).ToHashSet();

            // One batched load for every requested seat — replaces a per-seat SeatStore.GetByIdAsync +
            // SeatTypeStore.GetByIdAsync pair that used to run inside this loop.
            var seatIds  = request.Seats.Select(s => s.SeatId).Distinct().ToList();
            var seatsById = await _uow.SeatStore.GetByIdsAsync(seatIds);

            // A double seat must be booked whole: if any requested seat is part of a group, every seat
            // sharing that group must also be in this request, or the partner would be left half-sold.
            var groupIds = seatsById.Values.Where(s => s.SeatGroupId.HasValue).Select(s => s.SeatGroupId!.Value).Distinct().ToList();
            if (groupIds.Count > 0)
            {
                var requestedIds = seatIds.ToHashSet();
                var groups = (await _uow.SeatStore.FindAsync(s => s.SeatGroupId != null && groupIds.Contains(s.SeatGroupId!.Value)))
                    .GroupBy(s => s.SeatGroupId!.Value);
                foreach (var group in groups)
                {
                    if (!group.All(s => requestedIds.Contains(s.Id)))
                    {
                        throw new InvalidOperationException("A double seat must be booked together with its partner.");
                    }
                }
            }

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

                if (!seatsById.TryGetValue(seatItem.SeatId, out var seat))
                {
                    throw new KeyNotFoundException($"Seat {seatItem.SeatId} not found.");
                }
                var seatKind = seat.SeatGroupId.HasValue ? SeatKind.Double : SeatKind.Standard;

                // A PatronCategory is now the only source of a ticket's price, so every seat must name
                // one. The category's Price already has every pricing factor (RoomType override,
                // TimeSlot/Holiday, 3D, per-showtime BasePrice surcharge) resolved in — see
                // BuildSeatPricingContextAsync. Eligibility is enforced by requiring the category's own
                // seat kind to match the seat actually being booked: there is no separate allow-list.
                if (seatItem.PatronCategoryId is not Guid patronCategoryId || patronCategoryId == Guid.Empty)
                {
                    throw new InvalidOperationException("A patron category is required for every seat.");
                }
                if (!pricing.ByCategoryId.TryGetValue(patronCategoryId, out var category))
                {
                    throw new InvalidOperationException("Selected patron category is invalid or unavailable.");
                }
                if (category.Kind != seatKind)
                {
                    throw new InvalidOperationException($"Seat {seatItem.SeatId} is not available for the selected patron category.");
                }

                // A Double seat is one bookable unit priced once as a whole (category.Price is that
                // whole-seat price) but is still two physical Seat rows, each needing its own
                // InvoiceTicket for occupancy/refund tracking — split the price across the pair so
                // the two halves sum to the single price the customer was quoted for one seat.
                var price = seatKind == SeatKind.Double ? category.Price / 2 : category.Price;
                ticketTotal += price;

                // Unguessable per-ticket token; encoded as the e-ticket QR and checked at the gate.
                var qr = Guid.NewGuid().ToString("N");
                tickets.Add(new InvoiceTicket
                {
                    ShowTimeId            = request.ShowTimeId,
                    RoomId                = request.RoomId,
                    SeatId                = seatItem.SeatId,
                    Price                 = price,
                    PatronCategoryId      = category.Id,
                    PatronCategoryName    = category.Name,
                    PatronDiscountPercent = 0,
                    QrCode                = qr,
                });

                ticketItems.Add(new TicketItemDTO
                {
                    SeatLabel      = $"{seat.RowName}{seat.ColIndex}",
                    SeatType       = seatKind == SeatKind.Double ? "Double" : "Single",
                    Price          = price,
                    PatronCategory = category.Name,
                    QrCode         = qr,
                });
            }

            var (foods, foodTotal, stockDemand) = await BuildFoodLinesAndReserveStockAsync(request);

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
            await RecordSaleMovementsAsync(stockDemand, invoice, userId);
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

    // Units of one tracked item to take off the shelf for a booking, and the combos (if any) that asked for it.
    private sealed class StockDemand
    {
        public StockDemand(FoodAndDrink food)
        {
            Food = food;
        }

        public FoodAndDrink Food { get; }
        public int Quantity { get; set; }
        public List<string> ComboNames { get; } = new();
    }

    // Merges the requested food lines, validates them (theater + availability), expands combos into their
    // tracked components and takes the stock — all inside the caller's transaction, so a shortage throws and
    // the caller's catch rolls every stock change back. Fixed query count regardless of line count:
    // foods (1) + room (1) + combo recipes (1) + component foods (1).
    private async Task<(List<InvoiceFoodAndDrink> Lines, double Total, List<StockDemand> Demand)> BuildFoodLinesAndReserveStockAsync(CreateBookingRequest request)
    {
        var lines = new List<InvoiceFoodAndDrink>();
        var demand = new List<StockDemand>();
        if (request.Foods.Count == 0)
        {
            return (lines, 0, demand);
        }

        // The same food on two lines would collide on the (InvoiceId, FoodAndDrinkId) key; sum them.
        var merged = request.Foods
            .GroupBy(f => f.FoodAndDrinkId)
            .Select(g => (FoodAndDrinkId: g.Key, Quantity: g.Sum(x => x.Quantity)))
            .ToList();

        var room = await _uow.RoomStore.GetByIdAsync(request.RoomId);
        if (room is null)
        {
            throw new InvalidOperationException("ShowTime/Room combination not found.");
        }

        var foodsById = await _uow.FoodAndDrinkStore.GetByIdsAsync(merged.Select(m => m.FoodAndDrinkId).ToList());

        double total = 0;
        foreach (var (foodId, quantity) in merged)
        {
            if (!foodsById.TryGetValue(foodId, out var food))
            {
                throw new KeyNotFoundException($"Food item {foodId} not found.");
            }
            EnsureOrderable(food, room.TheaterId);

            lines.Add(new InvoiceFoodAndDrink
            {
                FoodAndDrinkId = foodId,
                Quantity       = quantity,
                UnitPrice      = food.Price,
                TotalPrice     = food.Price * quantity
            });
            total += food.Price * quantity;
        }

        var demandById = new Dictionary<Guid, StockDemand>();
        void Add(FoodAndDrink item, int units, string? comboName)
        {
            // Untracked items never touch stock.
            if (!item.TrackInventory)
            {
                return;
            }
            if (!demandById.TryGetValue(item.Id, out var entry))
            {
                entry = new StockDemand(item);
                demandById[item.Id] = entry;
            }
            entry.Quantity += units;
            if (comboName is not null && !entry.ComboNames.Contains(comboName))
            {
                entry.ComboNames.Add(comboName);
            }
        }

        var comboLines = merged.Where(m => foodsById[m.FoodAndDrinkId].IsCombo).ToList();
        foreach (var (foodId, quantity) in merged)
        {
            var food = foodsById[foodId];
            if (!food.IsCombo)
            {
                Add(food, quantity, null);
            }
        }

        if (comboLines.Count > 0)
        {
            var recipes = await _uow.ComboItemStore.GetByCombosAsync(comboLines.Select(c => c.FoodAndDrinkId).ToList());
            var componentIds = recipes.Select(r => r.ComponentId).Distinct().Where(id => !foodsById.ContainsKey(id)).ToList();
            var components = componentIds.Count == 0
                ? new Dictionary<Guid, FoodAndDrink>()
                : await _uow.FoodAndDrinkStore.GetByIdsAsync(componentIds);

            foreach (var (comboId, comboQuantity) in comboLines)
            {
                var combo = foodsById[comboId];
                foreach (var recipe in recipes.Where(r => r.ComboId == comboId))
                {
                    if (!foodsById.TryGetValue(recipe.ComponentId, out var component)
                        && !components.TryGetValue(recipe.ComponentId, out component))
                    {
                        throw new InvalidOperationException($"'{combo.Name}' is no longer available.");
                    }
                    EnsureOrderable(component, room.TheaterId);
                    Add(component, comboQuantity * recipe.Quantity, combo.Name);
                }
            }
        }

        // Fixed (ascending id) order so two bookings contending for the same items can't deadlock.
        var ordered = demandById.Values.OrderBy(d => d.Food.Id).ToList();
        foreach (var entry in ordered)
        {
            if (!await _uow.FoodAndDrinkStore.TryApplyStockDeltaAsync(entry.Food.Id, -entry.Quantity))
            {
                var name = entry.ComboNames.Count > 0 ? entry.ComboNames[0] : entry.Food.Name;
                throw new InvalidOperationException($"'{name}' is out of stock or has insufficient quantity.");
            }
        }

        return (lines, total, ordered);
    }

    private static void EnsureOrderable(FoodAndDrink food, Guid theaterId)
    {
        if (food.TheaterId != theaterId)
        {
            throw new InvalidOperationException($"'{food.Name}' is not available at this theater.");
        }
        if (!food.IsAvailable)
        {
            throw new InvalidOperationException($"'{food.Name}' is no longer available.");
        }
    }

    // One Sale ledger row per demanded item (negative quantity), written with the invoice in the same transaction.
    private async Task RecordSaleMovementsAsync(IReadOnlyList<StockDemand> demand, Invoice invoice, Guid userId)
    {
        if (demand.Count == 0)
        {
            return;
        }

        var movements = demand.Select(d => new StockMovement
        {
            FoodAndDrinkId = d.Food.Id,
            TheaterId      = d.Food.TheaterId,
            Type           = StockMovementType.Sale,
            Quantity       = -d.Quantity,
            Reason         = d.ComboNames.Count > 0
                ? $"Sold on {invoice.Code} via combo {string.Join(", ", d.ComboNames)}"
                : $"Sold on {invoice.Code}",
            InvoiceId      = invoice.Id,
            UserId         = userId,
        }).ToList();
        await _uow.StockMovementStore.CreateRangeAsync(movements);
    }

    // Puts sold stock back for invoices that did not complete. Ledger-driven and idempotent (see FoodStockRestorer).
    private Task RestoreFoodStockAsync(IReadOnlyCollection<Invoice> invoices, string reason, Guid? userId)
    {
        return FoodStockRestorer.RestoreAsync(_uow, invoices.Select(i => i.Id).ToList(), reason, userId);
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
    // A seat's price is anchored on the resolved PatronCategory row (theater-wide Price, or a
    // RoomType-specific override if one exists for this room's RoomType) — PatronCategory.Price is an
    // absolute, independently-configured amount per seat kind (Standard/Double), never derived from
    // another row. On top of that: the TicketPrice matrix's (theater/roomType/timeSlot/holiday) factor
    // multiplies it when a row matches (that row is already holiday-scoped, so the plain Holiday
    // factor is skipped in that case — only applied as a fallback when no matrix row matches); a 3D
    // screening adds RoomType.ThreeDSurcharge; and the showtime's own BasePrice is added last as a
    // manual per-showtime surcharge (e.g. a premium for a new release, 0 for a long-running title —
    // NOT a multiplier, so it never interacts with the factors above). The whole context is resolved
    // once per showtime+room and reused for every seat/category.
    private sealed record ResolvedCategory(Guid Id, string Name, Guid SeatTypeId, SeatKind Kind, double Price);

    private sealed record SeatPricingContext(
        IReadOnlyDictionary<Guid, ResolvedCategory> ByCategoryId,
        IReadOnlyDictionary<SeatKind, double> MinPriceByKind);

    private static readonly SeatPricingContext _emptyPricingContext =
        new(new Dictionary<Guid, ResolvedCategory>(), new Dictionary<SeatKind, double>());

    private async Task<SeatPricingContext> BuildSeatPricingContextAsync(ShowTimeRoom? showTimeRoom)
    {
        if (showTimeRoom is null)
        {
            return _emptyPricingContext;
        }

        var room     = await _uow.RoomStore.GetByIdAsync(showTimeRoom.RoomId);
        var showTime = await _uow.ShowTimeStore.GetByIdAsync(showTimeRoom.ShowTimeId);
        if (room is null || showTime is null)
        {
            return _emptyPricingContext;
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

        // Holiday: any Holiday whose date matches the showtime's date scales the price when no
        // TicketPrice matrix row matches (a matrix row is already holiday-scoped).
        var holidays  = await _uow.HolidayStore.FindAsync(h => h.Date == date) ?? Enumerable.Empty<Holiday>();
        var holiday   = holidays.FirstOrDefault();
        var isHoliday = holiday is not null;
        var fallbackHolidayFactor = isHoliday ? holiday!.PriceMultiplier : 1.0;

        // The theater time slot whose [start, end) window contains the showtime's time-of-day.
        var slots = await _uow.TimeSlotStore.FindAsync(t => t.TheaterId == room.TheaterId) ?? Enumerable.Empty<TimeSlot>();
        var slot  = slots.FirstOrDefault(s => TimeInSlot(timeOfDay, s));

        double? timeFactor = null;
        if (slot is not null)
        {
            var matrixRows = await _uow.TicketPriceStore.FindAsync(tp =>
                                  tp.TheaterId == room.TheaterId
                                  && tp.RoomTypeId == room.RoomTypeId
                                  && tp.TimeSlotId == slot.Id
                                  && tp.IsHoliday == isHoliday)
                              ?? Enumerable.Empty<TicketPrice>();
            timeFactor = matrixRows.Select(r => (double?)r.PriceMultiplier).FirstOrDefault();
        }
        var holidayFactor = timeFactor.HasValue ? 1.0 : fallbackHolidayFactor;

        var kindBySeatTypeId = (await _uow.SeatTypeStore.GetKindMapAsync(room.TheaterId))
            .ToDictionary(kv => kv.Value, kv => kv.Key);

        var categories = (await _uow.PatronCategoryStore.FindAsync(c => c.TheaterId == room.TheaterId && c.IsActive))
                          ?? Enumerable.Empty<PatronCategory>();
        var categoryList = categories.ToList();
        var categoryIds  = categoryList.Select(c => c.Id).ToList();

        var overridesForRoomType = categoryIds.Count == 0
            ? new List<RoomTypePatronCategoryPrice>()
            : (await _uow.RoomTypePatronCategoryPriceStore.FindByPatronCategoriesAsync(categoryIds))
                .Where(o => o.RoomTypeId == room.RoomTypeId)
                .ToList();
        var overrideByCategory = overridesForRoomType.ToDictionary(o => o.PatronCategoryId, o => o.Price);
        // RoomTypePatronCategoryPrice is a per-RoomType allow-list: a RoomType offers exactly the
        // categories that have a row here, at that row's price. Zero rows means the RoomType offers
        // nothing — there is no "unrestricted" fallback to the theater-wide PatronCategory.Price.
        var byCategoryId = new Dictionary<Guid, ResolvedCategory>();
        foreach (var c in categoryList)
        {
            if (!overrideByCategory.TryGetValue(c.Id, out var roomTypePrice))
            {
                continue;
            }
            var price = Math.Round(roomTypePrice * (timeFactor ?? 1.0) * holidayFactor + threeDSurcharge + showTimeRoom.BasePrice, 2);
            var kind  = kindBySeatTypeId.TryGetValue(c.SeatTypeId, out var k) ? k : SeatKind.Standard;
            byCategoryId[c.Id] = new ResolvedCategory(c.Id, c.Name, c.SeatTypeId, kind, price);
        }

        var minPriceByKind = byCategoryId.Values
            .GroupBy(c => c.Kind)
            .ToDictionary(g => g.Key, g => g.Min(c => c.Price));

        return new SeatPricingContext(byCategoryId, minPriceByKind);
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
        var running = await ApplyMembershipDiscountAsync(userId, total);

        var now = DateTime.UtcNow;
        var bookingTheaterId = (await _uow.RoomStore.GetByIdAsync(roomId))?.TheaterId;
        var showTime = await _uow.ShowTimeStore.GetByIdAsync(showTimeId);

        Guid? discountId = null;
        if (!string.IsNullOrWhiteSpace(discountCode))
        {
            var discount = await FindUsableDiscountAsync(discountCode.Trim(), now, bookingTheaterId, showTime);
            if (discount is null)
            {
                throw new InvalidOperationException(_invalidDiscountMessage);
            }

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

    private const string _invalidDiscountMessage = "Discount code is invalid or no longer available.";

    // The member's tier discount comes off first, before any promo code.
    private async Task<double> ApplyMembershipDiscountAsync(Guid userId, double running)
    {
        var user = await _uow.UserStore.GetByIdAsync(userId);
        if (user?.MemberShipId is Guid membershipId)
        {
            var membership = await _uow.MemberShipStore.GetByIdAsync(membershipId);
            if (membership is { DiscountPercent: > 0 })
            {
                running -= running * (membership.DiscountPercent / 100.0);
            }
        }
        return running;
    }

    // The promo code if it exists, is active, in its date window, under its usage cap and in scope for
    // this booking; otherwise null.
    private async Task<Discount?> FindUsableDiscountAsync(string code, DateTime now, Guid? bookingTheaterId, ShowTime? showTime)
    {
        var discount = await _uow.DiscountStore.GetByCodeAsync(code);
        if (discount is null
            || !discount.IsActive
            || discount.StartDate > now || now > discount.EndDate
            || (discount.MaxUsage != null && discount.UsedCount >= discount.MaxUsage)
            || !MatchesScope(discount, bookingTheaterId, showTime))
        {
            return null;
        }
        return discount;
    }

    public async Task<DiscountCodeValidationDTO> ValidateDiscountCodeAsync(Guid userId, string code, Guid roomId, Guid showTimeId, double total)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return new DiscountCodeValidationDTO { Valid = false, Message = _invalidDiscountMessage };
        }

        var bookingTheaterId = (await _uow.RoomStore.GetByIdAsync(roomId))?.TheaterId;
        var showTime = await _uow.ShowTimeStore.GetByIdAsync(showTimeId);
        var discount = await FindUsableDiscountAsync(code.Trim(), DateTime.UtcNow, bookingTheaterId, showTime);
        if (discount is null)
        {
            return new DiscountCodeValidationDTO { Valid = false, Message = _invalidDiscountMessage };
        }

        var running = await ApplyMembershipDiscountAsync(userId, total);
        var amount = Math.Round(Math.Min(ApplyPercent(running, discount), running), 2);
        return new DiscountCodeValidationDTO { Valid = true, DiscountAmount = amount };
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
        await _uow.BeginTransactionAsync();
        try
        {
            invoice.Status = InvoiceStatus.Cancelled;
            await _uow.InvoiceStore.UpdateAsync(invoice);
            await _uow.InvoiceStore.DeactivateTicketsAsync(invoice.Id);
            await RestoreRedeemedPointsAsync(invoice);
            await RestoreGiftCardAsync(invoice);
            await RestoreFoodStockAsync(new[] { invoice }, "Cancelled", userId);
            await _uow.SaveChangesAsync();
            await _uow.CommitTransactionAsync();
        }
        catch
        {
            await _uow.RollbackTransactionAsync();
            throw;
        }
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

        // The gateway call above can't be rolled back, so the DB writes start their transaction after it.
        User? user;
        await _uow.BeginTransactionAsync();
        try
        {
            // Mark refunded. Because seat occupancy counts only Pending/Paid invoices, this frees the seats.
            invoice.Status     = InvoiceStatus.Refunded;
            invoice.RefundedAt = DateTime.UtcNow;
            await _uow.InvoiceStore.UpdateAsync(invoice);
            await _uow.InvoiceStore.DeactivateTicketsAsync(invoice.Id);

            // Reverse the loyalty points accrued at payment, give back any points spent on this booking,
            // and re-evaluate the membership tier.
            user = invoice.User ?? await _uow.UserStore.GetByIdAsync(invoice.UserId);
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

            // Put the sold food/drink stock back.
            await RestoreFoodStockAsync(new[] { invoice }, "Refunded", userId);

            await _uow.SaveChangesAsync();
            await _uow.CommitTransactionAsync();
        }
        catch
        {
            await _uow.RollbackTransactionAsync();
            throw;
        }

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
        await _uow.BeginTransactionAsync();
        try
        {
            foreach (var invoice in stale)
            {
                // Cancelling frees the held seats — GetBookedSeatIdsAsync only counts Pending/Paid.
                invoice.Status = InvoiceStatus.Cancelled;
                await _uow.InvoiceStore.UpdateAsync(invoice);
                await _uow.InvoiceStore.DeactivateTicketsAsync(invoice.Id);
                await RestoreRedeemedPointsAsync(invoice);
                await RestoreGiftCardAsync(invoice);
            }
            // One batched ledger read/restock for the whole run, not one per invoice.
            await RestoreFoodStockAsync(stale.ToList(), "Expired", null);
            await _uow.SaveChangesAsync();
            await _uow.CommitTransactionAsync();
        }
        catch
        {
            await _uow.RollbackTransactionAsync();
            throw;
        }
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
