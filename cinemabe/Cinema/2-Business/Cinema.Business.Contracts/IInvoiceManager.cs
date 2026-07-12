using Cinema.Business.DTO.Invoices;
using Cinema.Business.DTO.Requests;
using Cinema.Data.Entities;
namespace Cinema.Business.Contracts;
public interface IInvoiceManager
{
    Task<DefaultSearchResults<InvoiceDTO>> GetMyInvoicesAsync(Guid userId, PagingSearchDTO search);
    Task<DefaultSearchResults<InvoiceDTO>> GetInvoicesAsync(PagingSearchDTO search);
    Task<InvoiceDTO>                       GetByIdAsync(Guid id, Guid requestingUserId, bool isAdmin);
    Task<double>                          GetTotalRevenueAsync(DateTime from, DateTime to);
    Task<List<RevenueByDayDTO>>            GetRevenueByDayAsync(DateTime from, DateTime to);
    Task<List<RevenueBreakdownDTO>>        GetRevenueByMovieAsync(DateTime from, DateTime to);
    Task<List<RevenueBreakdownDTO>>        GetRevenueByTheaterAsync(DateTime from, DateTime to);
}
