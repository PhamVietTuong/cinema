using Cinema.Data.Contexts;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Cinema.Data.Enums;
using Microsoft.EntityFrameworkCore;

namespace Cinema.Data.Stores;

public class CustomerServiceStore : ICustomerServiceStore
{
    private readonly CinemaContext _db;

    public CustomerServiceStore(CinemaContext db)
    {
        _db = db;
    }

    public async Task<CustomerRow?> FindCustomerByEmailAsync(string email, string customerTypeName)
    {
        return await ProjectCustomer(_db.User.AsNoTracking().Where(u => u.UserType.Name == customerTypeName && u.Email == email))
            .FirstOrDefaultAsync();
    }

    public async Task<CustomerRow?> FindCustomerByPhoneAsync(string phone, string customerTypeName)
    {
        return await ProjectCustomer(_db.User.AsNoTracking().Where(u => u.UserType.Name == customerTypeName && u.Phone == phone))
            .FirstOrDefaultAsync();
    }

    public async Task<CustomerRow?> FindCustomerByInvoiceCodeAsync(string invoiceCode, string customerTypeName)
    {
        var users = _db.Invoice.AsNoTracking()
            .Where(i => i.Code == invoiceCode && i.UserId != null)
            .Select(i => i.User!)
            .Where(u => u.UserType.Name == customerTypeName);
        return await ProjectCustomer(users).FirstOrDefaultAsync();
    }

    public async Task<List<CustomerInvoiceRow>> GetInvoicesAsync(Guid? userId, string? invoiceCode, IReadOnlyCollection<Guid>? theaterIds, int take)
    {
        var query = _db.Invoice.AsNoTracking().AsQueryable();
        if (userId.HasValue)
        {
            var id = userId.Value;
            query = query.Where(i => i.UserId == id);
        }
        if (!string.IsNullOrEmpty(invoiceCode))
        {
            query = query.Where(i => i.Code == invoiceCode);
        }
        if (theaterIds != null)
        {
            query = query.Where(i => i.TheaterId != null && theaterIds.Contains(i.TheaterId.Value));
        }

        var rows = await query
            .OrderByDescending(i => i.CreationTime)
            .Take(take)
            .Select(i => new
            {
                i.Id,
                i.Code,
                i.TheaterId,
                i.Status,
                i.Channel,
                i.FinalAmount,
                i.PaidAt,
                i.CreationTime,
                MovieTitle = i.InvoiceTickets.Select(t => t.ShowTimeRoom.ShowTime.Movie.Title).FirstOrDefault(),
                FirstShowStart = i.InvoiceTickets.Min(t => (DateTime?)t.ShowTimeRoom.ShowTime.StartTime),
                TicketCount = i.InvoiceTickets.Count()
            })
            .ToListAsync();

        var theaterIdsToName = rows.Where(r => r.TheaterId.HasValue).Select(r => r.TheaterId!.Value).Distinct().ToList();
        var names = theaterIdsToName.Count == 0
            ? new Dictionary<Guid, string>()
            : await _db.Theater.AsNoTracking()
                .Where(t => theaterIdsToName.Contains(t.Id))
                .Select(t => new { t.Id, t.Name })
                .ToDictionaryAsync(t => t.Id, t => t.Name);

        return rows.Select(r => new CustomerInvoiceRow(
            r.Id,
            r.Code,
            r.TheaterId,
            r.TheaterId.HasValue && names.TryGetValue(r.TheaterId.Value, out var theaterName) ? theaterName : null,
            r.Status,
            r.Channel,
            r.FinalAmount,
            r.PaidAt,
            r.CreationTime,
            r.MovieTitle,
            r.FirstShowStart,
            r.TicketCount)).ToList();
    }

    public async Task<ResendInvoiceRow?> GetResendInvoiceAsync(Guid invoiceId)
    {
        var row = await _db.Invoice.AsNoTracking()
            .Where(i => i.Id == invoiceId)
            .Select(i => new
            {
                i.Id,
                i.Code,
                i.TheaterId,
                i.Status,
                i.FinalAmount,
                UserEmail = i.User != null ? i.User.Email : null,
                UserPhone = i.User != null ? i.User.Phone : null,
                QrCodes = i.InvoiceTickets.Where(t => t.QrCode != null).Select(t => t.QrCode!).ToList()
            })
            .FirstOrDefaultAsync();
        if (row == null)
        {
            return null;
        }
        return new ResendInvoiceRow(row.Id, row.Code, row.TheaterId, row.Status, row.FinalAmount, row.UserEmail, row.UserPhone, row.QrCodes);
    }

    public async Task<InvoiceHeaderRow?> GetInvoiceHeaderAsync(Guid invoiceId)
    {
        return await _db.Invoice.AsNoTracking()
            .Where(i => i.Id == invoiceId)
            .Select(i => new InvoiceHeaderRow(i.Id, i.Code, i.TheaterId, i.UserId, i.Status, i.FinalAmount))
            .FirstOrDefaultAsync();
    }

    public async Task<int> CountRecentAuditsAsync(Guid entityId, AuditAction action, DateTime sinceUtc)
    {
        return await _db.AuditLog.AsNoTracking()
            .CountAsync(a => a.EntityId == entityId && a.Action == action && a.CreationTime >= sinceUtc);
    }

    private static IQueryable<CustomerRow> ProjectCustomer(IQueryable<User> users)
    {
        return users.Select(u => new CustomerRow(
            u.Id,
            u.Name,
            u.Email,
            u.Phone,
            u.MemberShip != null ? u.MemberShip.Name : null,
            u.Points));
    }
}
