using Cinema.Data.Contexts;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Cinema.Data.Stores;

public class ComplaintStore : GenericStore<Complaint>, IComplaintStore
{
    public ComplaintStore(CinemaContext db) : base(db)
    {
    }

    public async Task<(List<ComplaintRow> Items, int Total)> SearchAsync(ComplaintSearchCriteria criteria)
    {
        var query = DbSet.AsNoTracking();
        if (criteria.TheaterIds != null)
        {
            var theaterIds = criteria.TheaterIds;
            query = query.Where(c => theaterIds.Contains(c.TheaterId));
        }
        if (criteria.Status.HasValue)
        {
            var status = criteria.Status.Value;
            query = query.Where(c => c.Status == status);
        }
        if (criteria.Category.HasValue)
        {
            var category = criteria.Category.Value;
            query = query.Where(c => c.Category == category);
        }
        if (criteria.AssignedToUserId.HasValue)
        {
            var assignee = criteria.AssignedToUserId.Value;
            query = query.Where(c => c.AssignedToUserId == assignee);
        }
        if (criteria.CustomerUserId.HasValue)
        {
            var customer = criteria.CustomerUserId.Value;
            query = query.Where(c => c.CustomerUserId == customer);
        }
        if (criteria.InvoiceId.HasValue)
        {
            var invoice = criteria.InvoiceId.Value;
            query = query.Where(c => c.InvoiceId == invoice);
        }

        var total = await query.CountAsync();
        var page = await query
            .OrderByDescending(c => c.CreationTime)
            .ThenByDescending(c => c.Id)
            .Skip(criteria.PageIndex * criteria.PageSize)
            .Take(criteria.PageSize)
            .ToListAsync();
        return (await WithLabelsAsync(page), total);
    }

    public async Task<ComplaintRow?> GetRowAsync(Guid id)
    {
        var complaint = await DbSet.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        if (complaint == null)
        {
            return null;
        }
        return (await WithLabelsAsync(new List<Complaint> { complaint }))[0];
    }

    /// <summary>Customer names and invoice codes for a page of complaints: two batched lookups, never one per row.</summary>
    private async Task<List<ComplaintRow>> WithLabelsAsync(List<Complaint> complaints)
    {
        var customerIds = complaints.Where(c => c.CustomerUserId.HasValue).Select(c => c.CustomerUserId!.Value).Distinct().ToList();
        var invoiceIds = complaints.Where(c => c.InvoiceId.HasValue).Select(c => c.InvoiceId!.Value).Distinct().ToList();

        var customers = customerIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await Context.User.AsNoTracking()
                .Where(u => customerIds.Contains(u.Id))
                .Select(u => new { u.Id, u.Name })
                .ToDictionaryAsync(u => u.Id, u => u.Name);
        var invoices = invoiceIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await Context.Invoice.AsNoTracking()
                .Where(i => invoiceIds.Contains(i.Id))
                .Select(i => new { i.Id, i.Code })
                .ToDictionaryAsync(i => i.Id, i => i.Code);

        return complaints.Select(c => new ComplaintRow(
            c,
            c.CustomerUserId.HasValue && customers.TryGetValue(c.CustomerUserId.Value, out var customerName) ? customerName : null,
            c.InvoiceId.HasValue && invoices.TryGetValue(c.InvoiceId.Value, out var invoiceCode) ? invoiceCode : null)).ToList();
    }
}
