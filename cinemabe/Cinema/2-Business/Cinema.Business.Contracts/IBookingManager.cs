using Cinema.Business.Contracts.Payments;
using Cinema.Business.DTO.Booking;
using Cinema.Business.DTO.BoxOffice;
using Cinema.Business.DTO.Invoices;
using Cinema.Business.DTO.Requests;
using Cinema.Data.Entities;
namespace Cinema.Business.Contracts;
public interface IBookingManager
{
    Task<DefaultSearchResults<SeatDTO>> GetSeatsAsync(PagingSearchDTO search);
    /// <summary>The resolved price list for a showtime+room: one entry per active PatronCategory of the
    /// theater, with all pricing factors applied. Drives the booking UI's quantity picker directly.</summary>
    Task<List<ShowTimePriceDTO>>        GetShowTimePricesAsync(Guid showTimeId, Guid roomId);
    Task<BookingResultDTO>              CreateBookingAsync(Guid userId, CreateBookingRequest request);
    /// <summary>Counter sale core (called by <c>IBoxOfficeManager</c>): same seat checks, pricing, stock, points and gift
    /// card as <see cref="CreateBookingAsync"/>, but the invoice is created Paid with its tenders, in one transaction.</summary>
    Task<CounterSaleResultDTO>          SellAtCounterAsync(CounterSaleContext context);
    /// <summary>Side-effect-free price quote of a counter sale (no stock, points, gift-card or invoice writes).</summary>
    Task<CounterQuoteDTO>               QuoteCounterAsync(CounterSaleContext context);
    /// <summary>Checks a promo code against a showtime+room using the same rules as booking, and reports
    /// what it would take off <paramref name="total"/>. Never throws for an unusable code.</summary>
    Task<DiscountCodeValidationDTO>     ValidateDiscountCodeAsync(Guid userId, string code, Guid roomId, Guid showTimeId, double total);
    /// <summary>Starts a payment for the owner's Pending invoice via the chosen provider; returns the redirect/checkout info.</summary>
    Task<PaymentInitiationDTO?>         InitiatePaymentAsync(Guid userId, Guid invoiceId, string? provider, string? returnUrl, string? clientIp);
    Task<bool>                          ConfirmPaymentAsync(Guid userId, Guid invoiceId, string paymentReference);
    /// <summary>Server-to-server gateway callback (IPN/webhook): signature-verifies and finalizes payment. No owner check.
    /// Returns which gateway resolved the callback and what was decided, so the caller can build the right
    /// provider-specific acknowledgement (e.g. VNPay's required {RspCode, Message} IPN response).</summary>
    Task<PaymentCallbackHandlingResult> HandlePaymentCallbackAsync(string provider, IReadOnlyDictionary<string, string> callbackData);
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
