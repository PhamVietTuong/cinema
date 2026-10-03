using Cinema.Business.Contracts;
using Cinema.Business.DTO.Catalog;
using Cinema.Business.DTO.Requests;
using Cinema.Business.Extensions;
using Cinema.Business.Helpers;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;

namespace Cinema.Business.Managers;

public class FoodAndDrinkManager : IFoodAndDrinkManager
{
    protected readonly IApplicationUnitOfWork _uow;

    public FoodAndDrinkManager(IApplicationUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await _uow.FoodAndDrinkStore.ExistsAsync(e => e.Id == id);
    }

    private IQueryable<FoodAndDrink> GetFilteredFoodAndDrinkQuery(Dictionary<string, string>? filters)
    {
        var query = _uow.FoodAndDrinkStore.GetQuery();
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
                    query = _uow.FoodAndDrinkStore.FilterQuery(query, e => e.Name.Contains(keyword));
                    break;

                case "theaterId":
                    if (Guid.TryParse(filters[key], out var theaterId))
                    {
                        query = _uow.FoodAndDrinkStore.FilterQuery(query, e => e.TheaterId == theaterId);
                    }
                    break;

                case "isAvailable":
                    if (bool.TryParse(filters[key], out var isAvailable))
                    {
                        query = _uow.FoodAndDrinkStore.FilterQuery(query, e => e.IsAvailable == isAvailable);
                    }
                    break;
            }
        }
        return query;
    }

    private IQueryable<FoodAndDrink> ApplySort(IQueryable<FoodAndDrink> query, SortDTO? sort)
    {
        if (sort == null || string.IsNullOrEmpty(sort.Field))
        {
            return query;
        }

        return sort.Field switch
        {
            "name" => _uow.FoodAndDrinkStore.OrderQuery(query, e => e.Name, sort.Ascending),
            "isAvailable" => _uow.FoodAndDrinkStore.OrderQuery(query, e => e.IsAvailable, sort.Ascending),
            _ => query,
        };
    }

    public async Task<DefaultSearchResults<FoodAndDrinkDTO>> GetAsync(PagingSearchDTO search)
    {
        search ??= new PagingSearchDTO();
        var (page, pageSize) = PagingHelper.ResolvePaging(search);

        var query = GetFilteredFoodAndDrinkQuery(search.Filters);
        query = ApplySort(query, search.Sort);
        var total = await _uow.FoodAndDrinkStore.CountAsync(query);
        var items = await _uow.FoodAndDrinkStore.AllPageAsync(query, page - 1, pageSize);
        var result = PagingHelper.ToPagedResult<FoodAndDrink, FoodAndDrinkDTO>(items, total, page, pageSize);
        await ComboAvailability.PopulateAsync(_uow, items, result.Results.ToList());
        return result;
    }

    public async Task<FoodAndDrinkDTO> GetByIdAsync(Guid id)
    {
        var entity = await _uow.FoodAndDrinkStore.GetByIdAsync(id);
        if (entity == null)
        {
            throw new KeyNotFoundException($"FoodAndDrink {id} not found.");
        }
        var dto = entity.ToDTO<FoodAndDrink, FoodAndDrinkDTO>();
        await ComboAvailability.PopulateAsync(_uow, new[] { entity }, new[] { dto });
        return dto;
    }

    public async Task<FoodAndDrinkDTO> CreateAsync(CreateFoodAndDrinkRequest request)
    {
        var entity = request.ToNewEntity<CreateFoodAndDrinkRequest, FoodAndDrink>();
        await _uow.FoodAndDrinkStore.CreateAsync(entity);
        return entity.ToDTO<FoodAndDrink, FoodAndDrinkDTO>();
    }

    public async Task<FoodAndDrinkDTO> UpdateAsync(UpdateFoodAndDrinkRequest request)
    {
        var entity = await _uow.FoodAndDrinkStore.GetByIdAsync(request.Id);
        if (entity == null)
        {
            throw new KeyNotFoundException($"FoodAndDrink {request.Id} not found.");
        }
        if (request.TheaterId != entity.TheaterId)
        {
            await EnsureTheaterCanChangeAsync(entity);
        }
        entity.PatchEntity<FoodAndDrink, UpdateFoodAndDrinkRequest>(request);
        await _uow.FoodAndDrinkStore.UpdateAsync(entity);
        var dto = entity.ToDTO<FoodAndDrink, FoodAndDrinkDTO>();
        await ComboAvailability.PopulateAsync(_uow, new[] { entity }, new[] { dto });
        return dto;
    }

    public async Task DeleteAsync(Guid id)
    {
        var usedIn = await _uow.ComboItemStore.GetCombosUsingAsync(id);
        if (usedIn.Count > 0)
        {
            throw new InvalidOperationException($"Used in combo(s): {string.Join(", ", usedIn.Select(u => u.ComboName))}");
        }
        await _uow.FoodAndDrinkStore.DeleteAsync(id);
    }

    /// <summary>
    /// Combos, components and stock-bearing items are tied to their theater (recipes must stay within one
    /// theater, and the ledger is theater-scoped), so moving them is refused.
    /// </summary>
    private async Task EnsureTheaterCanChangeAsync(FoodAndDrink entity)
    {
        if (entity.IsCombo)
        {
            throw new InvalidOperationException("A combo cannot be moved to another theater.");
        }

        var usedIn = await _uow.ComboItemStore.GetCombosUsingAsync(entity.Id);
        if (usedIn.Count > 0)
        {
            throw new InvalidOperationException($"Cannot change theater: used in combo(s): {string.Join(", ", usedIn.Select(u => u.ComboName))}");
        }

        if (entity.TrackInventory || entity.QuantityOnHand > 0 || await _uow.StockMovementStore.ExistsAsync(m => m.FoodAndDrinkId == entity.Id))
        {
            throw new InvalidOperationException("Cannot change theater: the item has inventory or stock history.");
        }
    }
}
