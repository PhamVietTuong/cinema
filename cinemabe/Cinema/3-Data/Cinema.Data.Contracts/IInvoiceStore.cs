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
    /// <summary>Paid tickets whose showtime starts in [from, to) — with user + movie + seat — for reminders.</summary>
    Task<IReadOnlyList<InvoiceTicket>> GetPaidTicketsForShowtimesAsync(DateTime from, DateTime to);
    /// <summary>Marks an invoice's tickets inactive (frees their seats at the DB unique-index level).
    /// Called when a booking is cancelled, expires, or is refunded.</summary>
    Task DeactivateTicketsAsync(Guid invoiceId);
}
