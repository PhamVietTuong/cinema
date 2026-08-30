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

    private IQueryable<AgeRestriction> GetFilteredAgeRestrictionQuery(Dictionary<string, string>? filters)
    {
        var query = _uow.AgeRestrictionStore.GetQuery();
        if (filters == null)
        {
            return query;
        }

        foreach (var key in filters.Keys)
        {
            if (string.IsNullOrEmpty(filters[key]))
            {
                continue;
            }

            switch (key)
            {
                case "keyword":
                    var keyword = filters[key];
                    query = _uow.AgeRestrictionStore.FilterQuery(query, e => e.Code.Contains(keyword) || e.Description.Contains(keyword));
                    break;

                case "code":
                    var code = filters[key];
                    query = _uow.AgeRestrictionStore.FilterQuery(query, e => e.Code.Contains(code));
                    break;

                case "description":
                    var description = filters[key];
                    query = _uow.AgeRestrictionStore.FilterQuery(query, e => e.Description.Contains(description));
                    break;

                case "minAge":
                    if (int.TryParse(filters[key], out var minAge))
                    {
                        query = _uow.AgeRestrictionStore.FilterQuery(query, e => e.MinAge == minAge);
                    }
                    break;
            }
        }
        return query;
    }

    private IQueryable<AgeRestriction> ApplySort(IQueryable<AgeRestriction> query, SortDTO? sort)
    {
        if (sort == null || string.IsNullOrEmpty(sort.Field))
        {
            return query;
        }

        return sort.Field switch
        {
            "code" => _uow.AgeRestrictionStore.OrderQuery(query, e => e.Code, sort.Ascending),
            "description" => _uow.AgeRestrictionStore.OrderQuery(query, e => e.Description, sort.Ascending),
            "minAge" => _uow.AgeRestrictionStore.OrderQuery(query, e => e.MinAge, sort.Ascending),
            _ => query,
        };
    }

    public async Task<DefaultSearchResults<AgeRestrictionDTO>> GetAsync(PagingSearchDTO search)
    {
        search ??= new PagingSearchDTO();
        var (page, pageSize) = PagingHelper.ResolvePaging(search);

        var query = GetFilteredAgeRestrictionQuery(search.Filters);
        query = ApplySort(query, search.Sort);
        var total = await _uow.AgeRestrictionStore.CountAsync(query);
        var items = await _uow.AgeRestrictionStore.AllPageAsync(query, page - 1, pageSize);
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
