using Cinema.Business.DTO.Invoices;
using Cinema.Business.DTO.Requests;
using Cinema.Data.Entities;
namespace Cinema.Business.Contracts;
public interface IInvoiceManager
{
    Task<DefaultSearchResults<InvoiceDTO>> GetMyInvoicesAsync(Guid userId, PagingSearchDTO search);
    Task<DefaultSearchResults<InvoiceDTO>> GetInvoicesAsync(PagingSearchDTO search);
    Task<InvoiceDTO>                       GetByIdAsync(Guid id);
    Task<double>                          GetTotalRevenueAsync(DateTime from, DateTime to);
    Task<List<RevenueByDayDTO>>            GetRevenueByDayAsync(DateTime from, DateTime to);
}
