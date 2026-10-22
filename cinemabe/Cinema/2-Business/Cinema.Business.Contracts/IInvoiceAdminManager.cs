using Cinema.Business.DTO.Catalog;
using Cinema.Business.DTO.Requests;
using Cinema.Data.Entities;

namespace Cinema.Business.Contracts;

/// <summary>Admin view over invoices: list, change status, delete. Invoices are created by booking.</summary>
public interface IInvoiceAdminManager
{
    Task<DefaultSearchResults<InvoiceAdminDTO>> GetAsync(PagingSearchDTO search);
    Task<InvoiceAdminDTO> UpdateStatusAsync(UpdateInvoiceStatusRequest request);
    Task DeleteAsync(Guid id);
}
