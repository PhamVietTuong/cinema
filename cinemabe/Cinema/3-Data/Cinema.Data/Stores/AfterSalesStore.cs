using Cinema.Data.Contexts;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Cinema.Data.Enums;
using Microsoft.EntityFrameworkCore;

namespace Cinema.Data.Stores;

public class AfterSalesStore : IAfterSalesStore
{
    private readonly CinemaContext _db;

    public AfterSalesStore(CinemaContext db)
    {
        _db = db;
    }

    public async Task<bool> TryClaimRefundAsync(Guid invoiceId, StaffReasonCode? reason, DateTime nowUtc)
    {
        var rows = await _db.Invoice
            .Where(i => i.Id == invoiceId && i.Status == InvoiceStatus.Paid)
            .ExecuteUpdateAsync(s => s
                .SetProperty(i => i.Status, InvoiceStatus.Refunded)
                .SetProperty(i => i.RefundedAt, (DateTime?)nowUtc)
                .SetProperty(i => i.RefundReasonCode, reason));
        return rows == 1;
    }

    public async Task<List<AfterSalesInvoiceRow>> FindInvoicesAsync(Guid theaterId, string? code, string? phone, int take)
    {
        var query = _db.Invoice.AsNoTracking().Where(i => i.TheaterId == theaterId);
        if (!string.IsNullOrEmpty(code))
        {
            query = query.Where(i => i.Code == code);
        }
        if (!string.IsNullOrEmpty(phone))
        {
            query = query.Where(i => i.User != null && i.User.Phone == phone);
        }
        return await query
            .OrderByDescending(i => i.CreationTime)
            .Take(take)
            .Select(i => new AfterSalesInvoiceRow
            {
                Id = i.Id,
                Code = i.Code,
                Status = i.Status,
                Channel = i.Channel,
                FinalAmount = i.FinalAmount,
                PaidAt = i.PaidAt,
                RefundedAt = i.RefundedAt,
                CustomerName = i.User != null ? i.User.Name : null,
                CustomerPhone = i.User != null ? i.User.Phone : null,
                TicketCount = i.InvoiceTickets.Count(),
                UsedTicketCount = i.InvoiceTickets.Count(t => t.IsUsed),
                FoodCount = i.InvoiceFoodAndDrinks.Count(),
                FirstShowStart = i.InvoiceTickets.Min(t => (DateTime?)t.ShowTimeRoom.ShowTime.StartTime),
                MovieTitle = i.InvoiceTickets.Select(t => t.ShowTimeRoom.ShowTime.Movie.Title).FirstOrDefault(),
                ExchangedFromInvoiceId = i.ExchangedFromInvoiceId,
            })
            .ToListAsync();
    }

    public async Task<List<TenderTotalRow>> GetTenderTotalsAsync(Guid theaterId, DateTime fromUtc, DateTime toUtc)
    {
        return await _db.InvoicePayment
            .AsNoTracking()
            .Where(p => p.Invoice.TheaterId == theaterId
                        && p.Invoice.PaidAt >= fromUtc && p.Invoice.PaidAt < toUtc
                        && (p.Invoice.Status == InvoiceStatus.Paid || p.Invoice.Status == InvoiceStatus.Refunded))
            .GroupBy(p => p.Method)
            .Select(g => new TenderTotalRow { Method = g.Key, Amount = g.Sum(p => p.Amount), Count = g.Count() })
            .ToListAsync();
    }

    public async Task<List<RefundedInvoiceRow>> GetRefundedInvoicesAsync(Guid theaterId, DateTime fromUtc, DateTime toUtc)
    {
        return await _db.Invoice
            .AsNoTracking()
            .Where(i => i.TheaterId == theaterId && i.Status == InvoiceStatus.Refunded
                        && i.RefundedAt >= fromUtc && i.RefundedAt < toUtc)
            .Select(i => new RefundedInvoiceRow
            {
                Id = i.Id,
                FinalAmount = i.FinalAmount,
                ReplacementFinalAmount = _db.Invoice
                    .Where(n => n.ExchangedFromInvoiceId == i.Id)
                    .Select(n => (double?)n.FinalAmount)
                    .FirstOrDefault(),
            })
            .ToListAsync();
    }

    public async Task<SalesVolumeRow> GetSalesVolumeAsync(Guid theaterId, DateTime fromUtc, DateTime toUtc)
    {
        var tickets = await _db.InvoiceTicket
            .AsNoTracking()
            .CountAsync(t => t.Invoice.TheaterId == theaterId && t.Invoice.Status == InvoiceStatus.Paid
                             && t.Invoice.PaidAt >= fromUtc && t.Invoice.PaidAt < toUtc);
        var food = await _db.InvoiceFoodAndDrink
            .AsNoTracking()
            .Where(f => f.Invoice.TheaterId == theaterId && f.Invoice.Status == InvoiceStatus.Paid
                        && f.Invoice.PaidAt >= fromUtc && f.Invoice.PaidAt < toUtc)
            .GroupBy(f => 1)
            .Select(g => new { Quantity = g.Sum(f => f.Quantity), Revenue = g.Sum(f => f.TotalPrice) })
            .FirstOrDefaultAsync();
        return new SalesVolumeRow
        {
            TicketsSold = tickets,
            FoodItemsSold = food?.Quantity ?? 0,
            FoodRevenue = food?.Revenue ?? 0,
        };
    }

    public async Task<(int Count, double Amount)> GetAuditTotalsAsync(Guid theaterId, AuditAction action, DateTime fromUtc, DateTime toUtc)
    {
        var row = await _db.AuditLog
            .AsNoTracking()
            .Where(a => a.TheaterId == theaterId && a.Action == action && a.CreationTime >= fromUtc && a.CreationTime < toUtc)
            .GroupBy(a => 1)
            .Select(g => new { Count = g.Count(), Amount = g.Sum(a => a.Amount ?? 0) })
            .FirstOrDefaultAsync();
        return row is null ? (0, 0) : (row.Count, row.Amount);
    }

    public async Task<List<DrawerSessionRow>> GetDrawerSessionsAsync(Guid theaterId, DateTime fromUtc, DateTime toUtc)
    {
        return await _db.CashDrawerSession
            .AsNoTracking()
            .Where(s => s.TheaterId == theaterId && s.OpenedAt >= fromUtc && s.OpenedAt < toUtc)
            .OrderBy(s => s.OpenedAt)
            .Select(s => new DrawerSessionRow
            {
                Id = s.Id,
                TerminalName = s.TerminalName,
                UserId = s.UserId,
                UserName = _db.User.Where(u => u.Id == s.UserId).Select(u => u.Name).FirstOrDefault(),
                Status = s.Status,
                OpenedAt = s.OpenedAt,
                ClosedAt = s.ClosedAt,
                OpeningFloat = s.OpeningFloat,
                MovementTotal = s.Movements.Sum(m => (double?)m.Amount) ?? 0,
                ExpectedCash = s.ExpectedCash,
                CountedCash = s.CountedCash,
                Variance = s.Variance,
            })
            .ToListAsync();
    }
}
