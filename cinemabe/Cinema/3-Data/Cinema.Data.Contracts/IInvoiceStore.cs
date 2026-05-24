using Cinema.Data.Entities;
using Cinema.Data.Enums;

namespace Cinema.Data.Contracts;

public interface IInvoiceStore : IGenericStore<Invoice>
{
    Task<Invoice?> GetWithDetailsAsync(Guid id);
    Task<Invoice?> GetByCodeAsync(string code);
    Task<(IEnumerable<Invoice> Items, int Total)> GetByUserAsync(Guid userId, int page, int pageSize);
    Task<(IEnumerable<Invoice> Items, int Total)> GetPagedAsync(InvoiceStatus? status, DateTime? from, DateTime? to, int page, int pageSize);
    Task<decimal> GetTotalRevenueAsync(DateTime from, DateTime to);
}
