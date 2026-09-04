using Cinema.Business.Contracts;
using Cinema.Business.DTO.Catalog;
using Cinema.Business.DTO.Requests;
using Cinema.Business.Extensions;
using Cinema.Business.Helpers;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;

namespace Cinema.Business.Managers;

public class PatronCategoryManager : IPatronCategoryManager
{
    protected readonly IApplicationUnitOfWork _uow;

    public PatronCategoryManager(IApplicationUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await _uow.PatronCategoryStore.ExistsAsync(e => e.Id == id);
    }

    private IQueryable<PatronCategory> GetFilteredPatronCategoryQuery(Dictionary<string, string>? filters)
    {
        var query = _uow.PatronCategoryStore.GetQuery();
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
                    query = _uow.PatronCategoryStore.FilterQuery(query, e => e.Name.Contains(keyword));
                    break;

                case "theaterId":
                    if (Guid.TryParse(filters[key], out var theaterId))
                    {
                        query = _uow.PatronCategoryStore.FilterQuery(query, e => e.TheaterId == theaterId);
                    }
                    break;

                case "isActive":
                    if (bool.TryParse(filters[key], out var isActive))
                    {
                        query = _uow.PatronCategoryStore.FilterQuery(query, e => e.IsActive == isActive);
                    }
                    break;
            }
        }
        return query;
    }

    private IQueryable<PatronCategory> ApplySort(IQueryable<PatronCategory> query, SortDTO? sort)
    {
        if (sort == null || string.IsNullOrEmpty(sort.Field))
        {
            return query;
        }

        return sort.Field switch
        {
            "name"            => _uow.PatronCategoryStore.OrderQuery(query, e => e.Name, sort.Ascending),
            "discountPercent" => _uow.PatronCategoryStore.OrderQuery(query, e => e.DiscountPercent, sort.Ascending),
            _ => query,
        };
    }

    public async Task<DefaultSearchResults<PatronCategoryDTO>> GetAsync(PagingSearchDTO search)
    {
        search ??= new PagingSearchDTO();
        var (page, pageSize) = PagingHelper.ResolvePaging(search);

        var query = GetFilteredPatronCategoryQuery(search.Filters);
        query = ApplySort(query, search.Sort);
        var total = await _uow.PatronCategoryStore.CountAsync(query);
        var items = await _uow.PatronCategoryStore.AllPageAsync(query, page - 1, pageSize);
        return PagingHelper.ToPagedResult<PatronCategory, PatronCategoryDTO>(items, total, page, pageSize);
    }

    public async Task<PatronCategoryDTO> GetByIdAsync(Guid id)
    {
        var entity = await _uow.PatronCategoryStore.GetByIdAsync(id);
        if (entity == null)
        {
            throw new KeyNotFoundException($"PatronCategory {id} not found.");
        }
        return entity.ToDTO<PatronCategory, PatronCategoryDTO>();
    }

    public async Task<PatronCategoryDTO> CreateAsync(CreatePatronCategoryRequest request)
    {
        var entity = request.ToNewEntity<CreatePatronCategoryRequest, PatronCategory>();
        await _uow.PatronCategoryStore.CreateAsync(entity);
        return entity.ToDTO<PatronCategory, PatronCategoryDTO>();
    }

    public async Task<PatronCategoryDTO> UpdateAsync(UpdatePatronCategoryRequest request)
    {
        var entity = await _uow.PatronCategoryStore.GetByIdAsync(request.Id);
        if (entity == null)
        {
            throw new KeyNotFoundException($"PatronCategory {request.Id} not found.");
        }
        entity.PatchEntity<PatronCategory, UpdatePatronCategoryRequest>(request);
        await _uow.PatronCategoryStore.UpdateAsync(entity);
        return entity.ToDTO<PatronCategory, PatronCategoryDTO>();
    }

    public async Task DeleteAsync(Guid id)
    {
        await _uow.PatronCategoryStore.DeleteAsync(id);
    }
}
