using Cinema.Business.DTO.Booking;
using Cinema.Business.DTO.Invoices;
using Cinema.Business.DTO.Requests;
using Cinema.Data.Entities;
namespace Cinema.Business.Contracts;
public interface IBookingManager
{
    Task<DefaultSearchResults<SeatDTO>> GetSeatsAsync(PagingSearchDTO search);
    Task<BookingResultDTO>              CreateBookingAsync(Guid userId, CreateBookingRequest request);
    /// <summary>Starts a payment for the owner's Pending invoice via the chosen provider; returns the redirect/checkout info.</summary>
    Task<PaymentInitiationDTO?>         InitiatePaymentAsync(Guid userId, Guid invoiceId, string? provider, string? returnUrl);
    Task<bool>                          ConfirmPaymentAsync(Guid userId, Guid invoiceId, string paymentReference);
    /// <summary>Server-to-server gateway callback (IPN/webhook): signature-verifies and finalizes payment. No owner check.</summary>
    Task<bool>                          HandlePaymentCallbackAsync(string provider, IReadOnlyDictionary<string, string> callbackData);
    /// <summary>Gate check-in: validates a ticket QR, marks it used (once), returns its details.</summary>
    Task<TicketValidationDTO>           ValidateTicketAsync(string qrCode);
    Task<bool>                          CancelBookingAsync(Guid userId, Guid invoiceId);
    /// <summary>Cancels Pending invoices older than <paramref name="age"/> (frees their held seats). Returns the count expired.</summary>
    Task<int>                           ExpireStalePendingBookingsAsync(TimeSpan age);
    void LockSeat(Guid showTimeId, Guid roomId, Guid seatId, string connectionId);
    void UnlockSeat(Guid showTimeId, Guid roomId, Guid seatId, string connectionId);
    bool IsSeatLocked(Guid showTimeId, Guid roomId, Guid seatId, string? excludeConnectionId = null);
    /// <summary>Releases every seat still held by the given connection and returns the seats released.</summary>
    IReadOnlyList<(Guid ShowTimeId, Guid RoomId, Guid SeatId)> ReleaseConnectionLocks(string connectionId);
}
