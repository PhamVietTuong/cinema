using Cinema.Business.Contracts;
using Cinema.Business.DTO.Catalog;
using Cinema.Business.DTO.Requests;
using Cinema.Business.Extensions;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Cinema.Data.Enums;

namespace Cinema.Business.Managers;

public class InvoiceAdminManager : IInvoiceAdminManager
{
    private readonly IApplicationUnitOfWork _uow;

    public InvoiceAdminManager(IApplicationUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<DefaultSearchResults<InvoiceAdminDTO>> GetAsync(PagingSearchDTO search)
    {
        search ??= new PagingSearchDTO();
        var status = search.Filters.GetEnum<InvoiceStatus>("status");
        var page = search.PageIndex > 0 ? search.PageIndex : 1;
        var pageSize = search.PageSize > 0 ? search.PageSize : 20;

        var (items, total) = await _uow.InvoiceStore.GetPagedAsync(status, null, null, page, pageSize);
        var dtos = items.Select(Map).ToList();

        return new DefaultSearchResults<InvoiceAdminDTO>
        {
            Results = dtos,
            TotalCount = total,
            CountPerPage = pageSize,
            Page = page,
        };
    }

    public async Task<InvoiceAdminDTO> UpdateStatusAsync(UpdateInvoiceStatusRequest request)
    {
        var invoice = await _uow.InvoiceStore.GetByIdAsync(request.Id);
        if (invoice == null)
        {
            throw new KeyNotFoundException($"Invoice {request.Id} not found.");
        }
        invoice.Status = request.Status;
        if (request.Status == InvoiceStatus.Paid && invoice.PaidAt == null)
        {
            invoice.PaidAt = DateTime.UtcNow;
        }
        await _uow.InvoiceStore.UpdateAsync(invoice);
        return Map(invoice);
    }

    public async Task DeleteAsync(Guid id)
    {
        // A Pending invoice still holds food stock it took at booking time; put it back before the invoice
        // goes away. (Other statuses are completed sales, so their stock stays sold.)
        var invoice = await _uow.InvoiceStore.GetByIdAsync(id);
        if (invoice is null || invoice.Status != InvoiceStatus.Pending)
        {
            await _uow.InvoiceStore.DeleteAsync(id);
            return;
        }

        await _uow.BeginTransactionAsync();
        try
        {
            await FoodStockRestorer.RestoreAsync(_uow, new[] { id }, "Deleted", null);
            await _uow.InvoiceStore.DeleteAsync(id);
            await _uow.CommitTransactionAsync();
        }
        catch
        {
            await _uow.RollbackTransactionAsync();
            throw;
        }
    }

    private static InvoiceAdminDTO Map(Invoice i)
    {
        return new InvoiceAdminDTO
        {
            Id = i.Id,
            Code = i.Code,
            UserId = i.UserId,
            UserName = i.User?.Name ?? string.Empty,
            UserEmail = i.User?.Email ?? string.Empty,
            TotalAmount = i.TotalAmount,
            DiscountAmount = i.DiscountAmount,
            FinalAmount = i.FinalAmount,
            Status = i.Status,
            PaymentMethod = i.PaymentMethod,
            PaidAt = i.PaidAt,
            CreationTime = i.CreationTime,
        };
    }
}
