using Cinema.Business.DTO.Booking;
using Cinema.Business.DTO.Requests;
using Cinema.Data.Entities;
namespace Cinema.Business.Contracts;
public interface IBookingManager
{
    Task<DefaultSearchResults<SeatDTO>> GetSeatsAsync(PagingSearchDTO search);
    Task<BookingResultDTO>              CreateBookingAsync(Guid userId, CreateBookingRequest request);
    Task<bool>                          ConfirmPaymentAsync(Guid invoiceId, string paymentReference);
    Task<bool>                          CancelBookingAsync(Guid userId, Guid invoiceId);
    void LockSeat(Guid showTimeId, Guid roomId, Guid seatId, string connectionId);
    void UnlockSeat(Guid showTimeId, Guid roomId, Guid seatId, string connectionId);
    bool IsSeatLocked(Guid showTimeId, Guid roomId, Guid seatId, string? excludeConnectionId = null);
}
