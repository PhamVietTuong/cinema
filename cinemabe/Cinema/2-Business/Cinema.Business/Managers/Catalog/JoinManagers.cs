using Cinema.Business.Contracts;
using Cinema.Business.DTO.Catalog;
using Cinema.Business.DTO.Requests;
using Cinema.Business.Extensions;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Cinema.Data.Enums;

namespace Cinema.Business.Managers;

public class MovieTypeDetailManager(IApplicationUnitOfWork uow) : IMovieTypeDetailManager
{
    public async Task<DefaultSearchResults<MovieTypeDetailDTO>> GetAsync(PagingSearchDTO search)
    {
        search ??= new PagingSearchDTO();
        var all = (await uow.MovieTypeDetailStore.GetAllAsync())
            .Select(x => new MovieTypeDetailDTO
            {
                MovieId = x.MovieId,
                MovieTypeId = x.MovieTypeId,
                MovieTitle = x.Movie?.Title ?? string.Empty,
                MovieTypeName = x.MovieType?.Name ?? string.Empty,
            })
            .ToList();

        var page = search.PageIndex > 0 ? search.PageIndex : 1;
        var pageSize = search.PageSize > 0 ? search.PageSize : 20;
        var paged = all.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new DefaultSearchResults<MovieTypeDetailDTO>
        {
            Results = paged,
            TotalCount = all.Count,
            CountPerPage = pageSize,
            Page = page,
        };
    }

    public async Task<MovieTypeDetailDTO> CreateAsync(CreateMovieTypeDetailRequest request)
    {
        if (await uow.MovieTypeDetailStore.ExistsAsync(request.MovieId, request.MovieTypeId))
        {
            throw new InvalidOperationException("This movie / movie-type pairing already exists.");
        }
        await uow.MovieTypeDetailStore.AddAsync(new MovieTypeDetail
        {
            MovieId = request.MovieId,
            MovieTypeId = request.MovieTypeId,
        });
        return new MovieTypeDetailDTO { MovieId = request.MovieId, MovieTypeId = request.MovieTypeId };
    }

    public Task DeleteAsync(Guid movieId, Guid movieTypeId)
    {
        return uow.MovieTypeDetailStore.DeleteAsync(movieId, movieTypeId);
    }
}

public class InvoiceAdminManager(IApplicationUnitOfWork uow) : IInvoiceAdminManager
{
    public async Task<DefaultSearchResults<InvoiceAdminDTO>> GetAsync(PagingSearchDTO search)
    {
        search ??= new PagingSearchDTO();
        var status = search.Filters.GetEnum<InvoiceStatus>("status");
        var page = search.PageIndex > 0 ? search.PageIndex : 1;
        var pageSize = search.PageSize > 0 ? search.PageSize : 20;

        var (items, total) = await uow.InvoiceStore.GetPagedAsync(status, null, null, page, pageSize);
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
        var invoice = await uow.InvoiceStore.GetByIdAsync(request.Id)
                      ?? throw new KeyNotFoundException($"Invoice {request.Id} not found.");
        invoice.Status = request.Status;
        if (request.Status == InvoiceStatus.Paid && invoice.PaidAt == null)
        {
            invoice.PaidAt = DateTime.UtcNow;
        }
        await uow.InvoiceStore.UpdateAsync(invoice);
        return Map(invoice);
    }

    public Task DeleteAsync(Guid id)
    {
        return uow.InvoiceStore.DeleteAsync(id);
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
