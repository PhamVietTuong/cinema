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
    /// <summary>Counter sale core (called by <c>IBoxOfficeManager</c>): same seat checks, pricing, stock and points
    /// as <see cref="CreateBookingAsync"/>, but the invoice is created Paid with its tenders, in one transaction.</summary>
    Task<CounterSaleResultDTO>          SellAtCounterAsync(CounterSaleContext context);
    /// <summary>Side-effect-free price quote of a counter sale (no stock, points or invoice writes).</summary>
    Task<CounterQuoteDTO>               QuoteCounterAsync(CounterSaleContext context);
    /// <summary>Checks a promo code against a showtime+room using the same rules as booking, and reports
    /// what it would take off <paramref name="total"/> on a per-ticket-unit basis (built from
    /// <paramref name="seats"/>, the same seats already selected on the checkout page). Never throws for
    /// an unusable code.</summary>
    Task<DiscountCodeValidationDTO>     ValidateDiscountCodeAsync(Guid userId, string code, Guid roomId, Guid showTimeId, double total, List<BookingSeatItem> seats);
    /// <summary>Starts a payment for the owner's Pending invoice via the chosen provider; returns the redirect/checkout info.</summary>
    Task<PaymentInitiationDTO?>         InitiatePaymentAsync(Guid userId, Guid invoiceId, string? provider, string? returnUrl, string? clientIp);
    Task<bool>                          ConfirmPaymentAsync(Guid userId, Guid invoiceId, string paymentReference);
    /// <summary>Server-to-server gateway callback (IPN/webhook): signature-verifies and finalizes payment. No owner check.
    /// Returns which gateway resolved the callback and what was decided, so the caller can build the right
    /// provider-specific acknowledgement (e.g. VNPay's required {RspCode, Message} IPN response).</summary>
    Task<PaymentCallbackHandlingResult> HandlePaymentCallbackAsync(string provider, IReadOnlyDictionary<string, string> callbackData);
    /// <summary>Gate check-in: validates a ticket QR, marks it used (once), returns its details.</summary>
    Task<TicketValidationDTO>           ValidateTicketAsync(string qrCode, Guid adminUserId);
    /// <summary>Cancels Pending invoices older than <paramref name="age"/> (frees their held seats). Returns the count expired.</summary>
    Task<int>                           ExpireStalePendingBookingsAsync(TimeSpan age);
    /// <summary>Takes an advisory hold. The hold belongs to the (connection, user) pair: a connection id alone is not
    /// proof of ownership, because other clients can learn it and the booking API accepts it from the caller.</summary>
    /// <summary>Returns false when the connection already holds the maximum number of locks.</summary>
    bool LockSeat(Guid showTimeId, Guid roomId, Guid seatId, string connectionId, Guid? userId = null);
    /// <summary>True when a lock held by <paramref name="connectionId"/> and <paramref name="userId"/> was removed; false if there was none or it belongs to someone else.</summary>
    bool UnlockSeat(Guid showTimeId, Guid roomId, Guid seatId, string connectionId, Guid? userId = null);
    /// <summary>True while a live lock blocks the seat. A lock is ignored only when BOTH <paramref name="excludeConnectionId"/>
    /// and <paramref name="excludeUserId"/> match its owner, so replaying someone else's connection id does not bypass it.</summary>
    bool IsSeatLocked(Guid showTimeId, Guid roomId, Guid seatId, string? excludeConnectionId = null, Guid? excludeUserId = null);
    /// <summary>Releases every seat still held by the given connection and returns the seats released.</summary>
    IReadOnlyList<(Guid ShowTimeId, Guid RoomId, Guid SeatId)> ReleaseConnectionLocks(string connectionId);
    /// <summary>Removes every expired seat lock and returns the seats freed (for SeatUnlocked broadcasts).</summary>
    IReadOnlyList<(Guid ShowTimeId, Guid RoomId, Guid SeatId)> SweepExpiredLocks();
}
