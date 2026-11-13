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
    private const string AdminOverrideReference = "ADMIN-OVERRIDE";

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

        var from = invoice.Status;
        var to = request.Status;
        // Enum model binding accepts any integer; only the four named statuses may be written.
        if (!Enum.IsDefined(typeof(InvoiceStatus), to))
        {
            throw new InvalidOperationException($"Unknown invoice status {(int)to}.");
        }
        if (from == to)
        {
            return Map(invoice);
        }

        // A closed invoice cannot be reopened: its tickets were deactivated (seats freed) and may be rebooked.
        if (to == InvoiceStatus.Pending || (from != InvoiceStatus.Pending && to == InvoiceStatus.Paid))
        {
            throw new InvalidOperationException($"An invoice cannot move from {from} to {to}.");
        }

        await _uow.BeginTransactionAsync();
        try
        {
            // Atomic claim: if a payment callback or the expiry job changed the invoice meanwhile, do nothing.
            if (!await _uow.InvoiceStore.TryTransitionStatusAsync(invoice.Id, from, to))
            {
                throw new InvalidOperationException("The invoice status changed while it was being updated. Reload and retry.");
            }

            invoice.Status = to;
            if (to == InvoiceStatus.Paid)
            {
                // Manual settlement of a Pending invoice: same loyalty/promo effects and payment row as a gateway payment.
                invoice.PaidAt ??= DateTime.UtcNow;
                invoice.PaymentReference ??= AdminOverrideReference;
                if (invoice.FinalAmount > 0)
                {
                    // Empty Id so EF inserts it as a new child of the tracked invoice.
                    invoice.Payments.Add(new InvoicePayment
                    {
                        Id        = Guid.Empty,
                        Method    = PaymentTender.Cash,
                        Amount    = invoice.FinalAmount,
                        Reference = AdminOverrideReference,
                    });
                }
                await InvoiceLifecycle.ApplyPaidSideEffectsAsync(_uow, invoice);
            }
            else
            {
                // Cancelled/Failed: the seats must be freed and the food order closed, otherwise the tickets stay
                // scannable at the gate while the invoice says it is void.
                InvoiceLifecycle.CancelFoodOrder(invoice);
                await _uow.InvoiceStore.DeactivateTicketsAsync(invoice.Id);
                if (from == InvoiceStatus.Pending)
                {
                    // Nothing was paid, so what the booking reserved goes back to the customer.
                    await InvoiceLifecycle.RestoreRedeemedPointsAsync(_uow, invoice);
                    await FoodStockRestorer.RestoreAsync(_uow, new[] { invoice.Id }, "Cancelled", null);
                }
            }

            await _uow.InvoiceStore.UpdateAsync(invoice);
            await _uow.SaveChangesAsync();
            await _uow.CommitTransactionAsync();
        }
        catch
        {
            await _uow.RollbackTransactionAsync();
            throw;
        }
        return Map(invoice);
    }

    public async Task DeleteAsync(Guid id)
    {
        // A Pending invoice still holds what the booking reserved (food stock, redeemed points);
        // put it back before the invoice goes away. (Other statuses are completed sales, so those stay spent.)
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
            await InvoiceLifecycle.RestoreRedeemedPointsAsync(_uow, invoice);
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
