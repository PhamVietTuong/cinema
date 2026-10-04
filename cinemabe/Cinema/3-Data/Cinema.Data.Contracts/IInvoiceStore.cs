using Cinema.Data.Entities;
using Cinema.Data.Enums;

namespace Cinema.Data.Contracts;

public interface IInvoiceStore : IGenericStore<Invoice>
{
    Task<Invoice?> GetWithDetailsAsync(Guid id);
    Task<Invoice?> GetByCodeAsync(string code);
    Task<(IEnumerable<Invoice> Items, int Total)> GetByUserAsync(Guid userId, int page, int pageSize);
    Task<(IEnumerable<Invoice> Items, int Total)> GetPagedAsync(InvoiceStatus? status, DateTime? from, DateTime? to, int page, int pageSize);
    Task<double> GetTotalRevenueAsync(DateTime from, DateTime to);
    Task<IReadOnlyDictionary<DateTime, double>> GetRevenueByDayAsync(DateTime from, DateTime to);
    /// <summary>Ticket revenue grouped by movie title, over paid invoices in the range.</summary>
    Task<IReadOnlyDictionary<string, double>> GetRevenueByMovieAsync(DateTime from, DateTime to);
    /// <summary>Ticket revenue grouped by theater name, over paid invoices in the range.</summary>
    Task<IReadOnlyDictionary<string, double>> GetRevenueByTheaterAsync(DateTime from, DateTime to);
    /// <summary>Pending invoices created before <paramref name="olderThan"/> (abandoned/unpaid holds).</summary>
    Task<IReadOnlyList<Invoice>> GetStalePendingAsync(DateTime olderThan);
    /// <summary>Loads a ticket by its QR token, with invoice + seat + showtime/room details, for gate check-in.</summary>
    Task<InvoiceTicket?> GetTicketByQrAsync(string qrCode);
    /// <summary>No-tracking projection of a ticket by QR token with its invoice, seat, showtime, room theater and
    /// movie age rating, for gate scanning (one query).</summary>
    Task<GateTicketRow?> GetGateTicketByQrAsync(string qrCode);

    /// <summary>Atomically admits a ticket: a single UPDATE ... WHERE IsUsed = 0 AND IsActive = 1. Returns true only
    /// for the caller whose update changed the row, so two simultaneous scans cannot both admit.</summary>
    Task<bool> TryAdmitTicketAsync(Guid invoiceId, Guid seatId, Guid showTimeId, Guid userId, DateTime nowUtc);

    /// <summary>Paid, active tickets of a theater whose showtime starts in [dayStart, dayEnd), matched by exact
    /// invoice code and/or phone (no-tracking projection).</summary>
    Task<List<GateLookupRow>> FindTicketsForLookupAsync(Guid theaterId, string? invoiceCode, string? phone, DateTime dayStart, DateTime dayEnd);

    /// <summary>Paid tickets whose showtime starts in [from, to) — with user + movie + seat — for reminders.</summary>
    Task<IReadOnlyList<InvoiceTicket>> GetPaidTicketsForShowtimesAsync(DateTime from, DateTime to);
    /// <summary>Marks an invoice's tickets inactive (frees their seats at the DB unique-index level).
    /// Called when a booking is cancelled, expires, or is refunded.</summary>
    Task DeactivateTicketsAsync(Guid invoiceId);

    /// <summary>The pickup queue of a theater: Paid invoices whose food is Pending, Preparing or Ready, projected in one
    /// query with their food lines. An invoice with tickets is listed when its earliest showtime starts in
    /// [dayStart, dayEnd); a food-only sale is always listed. Unordered: the caller sorts.</summary>
    Task<List<PickupOrderRow>> GetPickupQueueAsync(Guid theaterId, DateTime dayStart, DateTime dayEnd);

    /// <summary>One invoice with food by its code, in the given theater (null when unknown, foodless or elsewhere).</summary>
    Task<PickupOrderRow?> GetPickupOrderByCodeAsync(Guid theaterId, string invoiceCode);

    /// <summary>One invoice with food by id (null when unknown or foodless).</summary>
    Task<PickupOrderRow?> GetPickupOrderByIdAsync(Guid invoiceId);

    /// <summary>The pickup header of an invoice, no tracking (null when unknown).</summary>
    Task<FoodOrderHeaderRow?> GetFoodOrderHeaderAsync(Guid invoiceId);

    /// <summary>Atomic compare-and-set of <c>FoodStatus</c> (UPDATE ... WHERE FoodStatus = from). Stamps the handover
    /// time and user when moving to HandedOver. False when the invoice was not in <paramref name="from"/> any more.</summary>
    Task<bool> TrySetFoodStatusAsync(Guid invoiceId, FoodOrderStatus from, FoodOrderStatus to, Guid userId, DateTime nowUtc);

    /// <summary>Invoice code by invoice id for a batch of ids (one query). Unknown ids are absent.</summary>
    Task<Dictionary<Guid, string>> GetCodesByIdsAsync(IReadOnlyCollection<Guid> ids);
}
