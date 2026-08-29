using System.Linq.Expressions;
using Cinema.Business.Contracts;
using Cinema.Business.DTO.Catalog;
using Cinema.Business.DTO.Requests;
using Cinema.Business.Extensions;
using Cinema.Business.Helpers;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;

namespace Cinema.Business.Managers;

public class DiscountTypeManager : IDiscountTypeManager
{
    protected readonly IApplicationUnitOfWork _uow;

    public DiscountTypeManager(IApplicationUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await _uow.DiscountTypeStore.ExistsAsync(e => e.Id == id);
    }

    public async Task<DefaultSearchResults<DiscountTypeDTO>> GetAsync(PagingSearchDTO search)
    {
        search ??= new PagingSearchDTO();
        var (page, pageSize) = PagingHelper.ResolvePaging(search);
        var keyword = search.Filters.GetString("keyword");

        Expression<Func<DiscountType, bool>> predicate = e =>
            string.IsNullOrEmpty(keyword) || e.Name.Contains(keyword!);

        var total = await _uow.DiscountTypeStore.CountAsync(predicate);
        var items = await _uow.DiscountTypeStore.FindAllPageAsync(page - 1, pageSize, predicate);
        return PagingHelper.ToPagedResult<DiscountType, DiscountTypeDTO>(items, total, page, pageSize);
    }

    public async Task<DiscountTypeDTO> GetByIdAsync(Guid id)
    {
        var entity = await _uow.DiscountTypeStore.GetByIdAsync(id);
        if (entity == null)
        {
            throw new KeyNotFoundException($"DiscountType {id} not found.");
        }
        return entity.ToDTO<DiscountType, DiscountTypeDTO>();
    }

    public async Task<DiscountTypeDTO> CreateAsync(CreateDiscountTypeRequest request)
    {
        var entity = request.ToNewEntity<CreateDiscountTypeRequest, DiscountType>();
        await _uow.DiscountTypeStore.CreateAsync(entity);
        return entity.ToDTO<DiscountType, DiscountTypeDTO>();
    }

    public async Task<DiscountTypeDTO> UpdateAsync(UpdateDiscountTypeRequest request)
    {
        var entity = await _uow.DiscountTypeStore.GetByIdAsync(request.Id);
        if (entity == null)
        {
            throw new KeyNotFoundException($"DiscountType {request.Id} not found.");
        }
        entity.PatchEntity<DiscountType, UpdateDiscountTypeRequest>(request);
        await _uow.DiscountTypeStore.UpdateAsync(entity);
        return entity.ToDTO<DiscountType, DiscountTypeDTO>();
    }

    public async Task DeleteAsync(Guid id)
    {
        await _uow.DiscountTypeStore.DeleteAsync(id);
    }
}
