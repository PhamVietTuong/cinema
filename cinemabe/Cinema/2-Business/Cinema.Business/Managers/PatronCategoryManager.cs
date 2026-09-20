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
            "name"  => _uow.PatronCategoryStore.OrderQuery(query, e => e.Name, sort.Ascending),
            "price" => _uow.PatronCategoryStore.OrderQuery(query, e => e.Price, sort.Ascending),
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
        await AttachSeatTypeInfoAsync(result.Results);
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
        await AttachSeatTypeInfoAsync(new[] { dto });
        return dto;
    }

    public async Task<List<PatronCategoryDTO>> GetByTheaterAsync(Guid theaterId)
    {
        var entities = (await _uow.PatronCategoryStore.FindAsync(c => c.TheaterId == theaterId)).ToList();
        var dtos = entities.Select(e => e.ToDTO<PatronCategory, PatronCategoryDTO>()).ToList();
        await AttachSeatTypeInfoAsync(dtos);
        return dtos;
    }

    public async Task<List<PatronCategoryDTO>> CreateAsync(CreatePatronCategoryRequest request)
    {
        ValidatePrices(request.Prices);
        await ValidateSeatTypesAsync(request.TheaterId, request.Prices);

        var created = new List<PatronCategory>();
        await _uow.BeginTransactionAsync();
        try
        {
            foreach (var item in request.Prices)
            {
                var entity = new PatronCategory
                {
                    TheaterId   = request.TheaterId,
                    SeatTypeId  = item.SeatTypeId,
                    Name        = request.Name,
                    Description = request.Description,
                    Price       = item.Price,
                    IsActive    = request.IsActive,
                };
                await _uow.PatronCategoryStore.CreateAsync(entity);
                created.Add(entity);
            }
            await _uow.CommitTransactionAsync();
        }
        catch
        {
            await _uow.RollbackTransactionAsync();
            throw;
        }

        var dtos = created.Select(e => e.ToDTO<PatronCategory, PatronCategoryDTO>()).ToList();
        await AttachSeatTypeInfoAsync(dtos);
        return dtos;
    }

    public async Task<List<PatronCategoryDTO>> UpdateAsync(UpdatePatronCategoryRequest request)
    {
        var anchor = await _uow.PatronCategoryStore.GetByIdAsync(request.Id);
        if (anchor == null)
        {
            throw new KeyNotFoundException($"PatronCategory {request.Id} not found.");
        }
        ValidatePrices(request.Prices);
        await ValidateSeatTypesAsync(request.TheaterId, request.Prices);

        var siblings = (await _uow.PatronCategoryStore.FindAsync(
            c => c.TheaterId == anchor.TheaterId && c.Name == anchor.Name)).ToList();
        var bySeatType = siblings.ToDictionary(s => s.SeatTypeId);
        var keepSeatTypeIds = request.Prices.Select(p => p.SeatTypeId).ToHashSet();

        var result = new List<PatronCategory>();
        await _uow.BeginTransactionAsync();
        try
        {
            foreach (var item in request.Prices)
            {
                if (bySeatType.TryGetValue(item.SeatTypeId, out var existing))
                {
                    existing.Name        = request.Name;
                    existing.Description = request.Description;
                    existing.Price       = item.Price;
                    existing.IsActive    = request.IsActive;
                    await _uow.PatronCategoryStore.UpdateAsync(existing);
                    result.Add(existing);
                }
                else
                {
                    var created = new PatronCategory
                    {
                        TheaterId   = request.TheaterId,
                        SeatTypeId  = item.SeatTypeId,
                        Name        = request.Name,
                        Description = request.Description,
                        Price       = item.Price,
                        IsActive    = request.IsActive,
                    };
                    await _uow.PatronCategoryStore.CreateAsync(created);
                    result.Add(created);
                }
            }

            // A sibling row for a seat kind no longer in Prices means the category should no longer be
            // able to book that kind — remove the row (its RoomTypePatronCategoryPrice overrides cascade).
            foreach (var stale in siblings.Where(s => !keepSeatTypeIds.Contains(s.SeatTypeId)))
            {
                await _uow.PatronCategoryStore.DeleteAsync(stale);
            }

            await _uow.CommitTransactionAsync();
        }
        catch
        {
            await _uow.RollbackTransactionAsync();
            throw;
        }

        var dtos = result.Select(e => e.ToDTO<PatronCategory, PatronCategoryDTO>()).ToList();
        await AttachSeatTypeInfoAsync(dtos);
        return dtos;
    }

    public async Task DeleteAsync(Guid id)
    {
        var anchor = await _uow.PatronCategoryStore.GetByIdAsync(id);
        if (anchor == null)
        {
            throw new KeyNotFoundException($"PatronCategory {id} not found.");
        }
        var siblings = await _uow.PatronCategoryStore.FindAsync(
            c => c.TheaterId == anchor.TheaterId && c.Name == anchor.Name);

        await _uow.BeginTransactionAsync();
        try
        {
            foreach (var sibling in siblings)
            {
                await _uow.PatronCategoryStore.DeleteAsync(sibling);
            }
            await _uow.CommitTransactionAsync();
        }
        catch
        {
            await _uow.RollbackTransactionAsync();
            throw;
        }
    }

    private static void ValidatePrices(List<PatronCategoryPriceItem> prices)
    {
        if (prices.Count == 0)
        {
            throw new InvalidOperationException("At least one seat-kind price is required.");
        }
        if (prices.Select(p => p.SeatTypeId).Distinct().Count() != prices.Count)
        {
            throw new InvalidOperationException("Each seat kind may appear only once.");
        }
    }

    private async Task ValidateSeatTypesAsync(Guid theaterId, List<PatronCategoryPriceItem> prices)
    {
        var ids = prices.Select(p => p.SeatTypeId).Distinct().ToList();
        var validCount = await _uow.SeatTypeStore.CountAsync(
            _uow.SeatTypeStore.GetQuery().Where(st => ids.Contains(st.Id) && st.TheaterId == theaterId));
        if (validCount != ids.Count)
        {
            throw new InvalidOperationException("One or more seat kinds do not belong to this theater.");
        }
    }

    private async Task AttachSeatTypeInfoAsync(IEnumerable<PatronCategoryDTO> items)
    {
        var itemList = items.ToList();
        var seatTypeIds = itemList.Select(x => x.SeatTypeId).Distinct().ToList();
        if (seatTypeIds.Count == 0)
        {
            return;
        }

        var seatTypes = (await _uow.SeatTypeStore.FindAsync(s => seatTypeIds.Contains(s.Id)))
            .ToDictionary(s => s.Id);

        foreach (var item in itemList)
        {
            if (seatTypes.TryGetValue(item.SeatTypeId, out var seatType))
            {
                item.SeatTypeName = seatType.Name;
                item.Kind         = seatType.Kind;
            }
        }
    }
}
