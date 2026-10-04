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

    /// <summary>Invoice code by invoice id for a batch of ids (one query). Unknown ids are absent.</summary>
    Task<Dictionary<Guid, string>> GetCodesByIdsAsync(IReadOnlyCollection<Guid> ids);
}
