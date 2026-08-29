using System.Linq.Expressions;
using Cinema.Business.Contracts;
using Cinema.Business.DTO.Catalog;
using Cinema.Business.DTO.Requests;
using Cinema.Business.Extensions;
using Cinema.Business.Helpers;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;

namespace Cinema.Business.Managers;

public class AgeRestrictionManager : IAgeRestrictionManager
{
    protected readonly IApplicationUnitOfWork _uow;

    public AgeRestrictionManager(IApplicationUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await _uow.AgeRestrictionStore.ExistsAsync(e => e.Id == id);
    }

    public async Task<DefaultSearchResults<AgeRestrictionDTO>> GetAsync(PagingSearchDTO search)
    {
        search ??= new PagingSearchDTO();
        var (page, pageSize) = PagingHelper.ResolvePaging(search);
        var keyword = search.Filters.GetString("keyword");

        Expression<Func<AgeRestriction, bool>> predicate = e =>
            string.IsNullOrEmpty(keyword) || e.Code.Contains(keyword!) || e.Description.Contains(keyword!);

        var total = await _uow.AgeRestrictionStore.CountAsync(predicate);
        var items = await _uow.AgeRestrictionStore.FindAllPageAsync(page - 1, pageSize, predicate);
        return PagingHelper.ToPagedResult<AgeRestriction, AgeRestrictionDTO>(items, total, page, pageSize);
    }

    public async Task<AgeRestrictionDTO> GetByIdAsync(Guid id)
    {
        var entity = await _uow.AgeRestrictionStore.GetByIdAsync(id);
        if (entity == null)
        {
            throw new KeyNotFoundException($"AgeRestriction {id} not found.");
        }
        return entity.ToDTO<AgeRestriction, AgeRestrictionDTO>();
    }

    public async Task<AgeRestrictionDTO> CreateAsync(CreateAgeRestrictionRequest request)
    {
        var entity = request.ToNewEntity<CreateAgeRestrictionRequest, AgeRestriction>();
        await _uow.AgeRestrictionStore.CreateAsync(entity);
        return entity.ToDTO<AgeRestriction, AgeRestrictionDTO>();
    }

    public async Task<AgeRestrictionDTO> UpdateAsync(UpdateAgeRestrictionRequest request)
    {
        var entity = await _uow.AgeRestrictionStore.GetByIdAsync(request.Id);
        if (entity == null)
        {
            throw new KeyNotFoundException($"AgeRestriction {request.Id} not found.");
        }
        entity.PatchEntity<AgeRestriction, UpdateAgeRestrictionRequest>(request);
        await _uow.AgeRestrictionStore.UpdateAsync(entity);
        return entity.ToDTO<AgeRestriction, AgeRestrictionDTO>();
    }

    public async Task DeleteAsync(Guid id)
    {
        await _uow.AgeRestrictionStore.DeleteAsync(id);
    }
}
