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
        var result = PagingHelper.ToPagedResult<PatronCategory, PatronCategoryDTO>(items, total, page, pageSize);
        await AttachAllowedSeatTypesAsync(result.Results);
        return result;
    }

    public async Task<PatronCategoryDTO> GetByIdAsync(Guid id)
    {
        var entity = await _uow.PatronCategoryStore.GetByIdAsync(id);
        if (entity == null)
        {
            throw new KeyNotFoundException($"PatronCategory {id} not found.");
        }
        var dto = entity.ToDTO<PatronCategory, PatronCategoryDTO>();
        await AttachAllowedSeatTypesAsync(new[] { dto });
        return dto;
    }

    public async Task<PatronCategoryDTO> CreateAsync(CreatePatronCategoryRequest request)
    {
        await ValidateAllowedSeatTypesAsync(request.TheaterId, request.AllowedSeatTypeIds);
        var entity = request.ToNewEntity<CreatePatronCategoryRequest, PatronCategory>();

        // The category row and its junction rows are two separate SaveChanges calls (GenericStore.
        // CreateAsync commits immediately) — wrap them in one transaction so a failure never leaves a
        // persisted category with no junction rows, which this design reads as "unrestricted".
        await _uow.BeginTransactionAsync();
        try
        {
            await _uow.PatronCategoryStore.CreateAsync(entity);
            await _uow.PatronCategorySeatTypeStore.ReplaceForPatronCategoryAsync(entity.Id, request.AllowedSeatTypeIds);
            await _uow.CommitTransactionAsync();
        }
        catch
        {
            await _uow.RollbackTransactionAsync();
            throw;
        }

        var dto = entity.ToDTO<PatronCategory, PatronCategoryDTO>();
        dto.AllowedSeatTypeIds = request.AllowedSeatTypeIds.Distinct().ToList();
        return dto;
    }

    public async Task<PatronCategoryDTO> UpdateAsync(UpdatePatronCategoryRequest request)
    {
        var entity = await _uow.PatronCategoryStore.GetByIdAsync(request.Id);
        if (entity == null)
        {
            throw new KeyNotFoundException($"PatronCategory {request.Id} not found.");
        }
        await ValidateAllowedSeatTypesAsync(request.TheaterId, request.AllowedSeatTypeIds);
        entity.PatchEntity<PatronCategory, UpdatePatronCategoryRequest>(request);

        await _uow.BeginTransactionAsync();
        try
        {
            await _uow.PatronCategoryStore.UpdateAsync(entity);
            await _uow.PatronCategorySeatTypeStore.ReplaceForPatronCategoryAsync(entity.Id, request.AllowedSeatTypeIds);
            await _uow.CommitTransactionAsync();
        }
        catch
        {
            await _uow.RollbackTransactionAsync();
            throw;
        }

        var dto = entity.ToDTO<PatronCategory, PatronCategoryDTO>();
        dto.AllowedSeatTypeIds = request.AllowedSeatTypeIds.Distinct().ToList();
        return dto;
    }

    public async Task DeleteAsync(Guid id)
    {
        await _uow.PatronCategoryStore.DeleteAsync(id);
    }

    private async Task ValidateAllowedSeatTypesAsync(Guid theaterId, List<Guid> seatTypeIds)
    {
        if (seatTypeIds.Count == 0)
        {
            return;
        }

        var distinctIds = seatTypeIds.Distinct().ToList();
        var validCount = await _uow.SeatTypeStore.CountAsync(
            _uow.SeatTypeStore.GetQuery().Where(st => distinctIds.Contains(st.Id) && st.TheaterId == theaterId));
        if (validCount != distinctIds.Count)
        {
            throw new InvalidOperationException("One or more allowed seat types do not belong to this theater.");
        }
    }

    private async Task AttachAllowedSeatTypesAsync(IEnumerable<PatronCategoryDTO> items)
    {
        var ids = items.Select(x => x.Id).ToList();
        if (ids.Count == 0)
        {
            return;
        }

        var rows = await _uow.PatronCategorySeatTypeStore.FindByPatronCategoriesAsync(ids);
        var byCategory = rows
            .GroupBy(x => x.PatronCategoryId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.SeatTypeId).ToList());

        foreach (var item in items)
        {
            item.AllowedSeatTypeIds = byCategory.TryGetValue(item.Id, out var seatTypeIds) ? seatTypeIds : new List<Guid>();
        }
    }
}
