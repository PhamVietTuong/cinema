using System.Collections.Concurrent;
using Cinema.Business.Contracts;
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

    public BookingManager(IApplicationUnitOfWork uow) => _uow = uow;

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
                Price         = showTimeRoom?.BasePrice ?? 0,
                IsLocked      = isLocked && !isBooked
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

                var price = (double)showTimeRoom.BasePrice;
                ticketTotal += price;

                tickets.Add(new InvoiceTicket
                {
                    ShowTimeId   = request.ShowTimeId,
                    RoomId       = request.RoomId,
                    SeatId       = seatItem.SeatId,
                    TicketTypeId = seatItem.TicketTypeId,
                    Price        = price,
                });

                ticketItems.Add(new TicketItemDTO
                {
                    SeatLabel = $"{seat.RowName}{seat.ColIndex}",
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

            var total   = ticketTotal + foodTotal;
            var invoice = new Invoice
            {
                Code                = GenerateCode(),
                UserId              = userId,
                TotalAmount         = total,
                DiscountAmount      = 0,
                FinalAmount         = total,
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
                DiscountAmount = 0,
                FinalAmount    = total,
                Status         = InvoiceStatus.Pending,
                Tickets        = ticketItems
            };
        }
        catch
        {
            await _uow.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task<bool> ConfirmPaymentAsync(Guid invoiceId, string paymentReference)
    {
        var invoice = await _uow.InvoiceStore.GetByIdAsync(invoiceId);
        if (invoice == null) return false;
        invoice.Status           = InvoiceStatus.Paid;
        invoice.PaymentReference = paymentReference;
        invoice.PaidAt           = DateTime.UtcNow;
        await _uow.InvoiceStore.UpdateAsync(invoice);
        await _uow.SaveChangesAsync();
        return true;
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
