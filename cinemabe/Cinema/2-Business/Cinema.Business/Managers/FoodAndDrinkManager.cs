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
        return PagingHelper.ToPagedResult<FoodAndDrink, FoodAndDrinkDTO>(items, total, page, pageSize);
    }

    public async Task<FoodAndDrinkDTO> GetByIdAsync(Guid id)
    {
        var entity = await _uow.FoodAndDrinkStore.GetByIdAsync(id);
        if (entity == null)
        {
            throw new KeyNotFoundException($"FoodAndDrink {id} not found.");
        }
        return entity.ToDTO<FoodAndDrink, FoodAndDrinkDTO>();
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
        entity.PatchEntity<FoodAndDrink, UpdateFoodAndDrinkRequest>(request);
        await _uow.FoodAndDrinkStore.UpdateAsync(entity);
        return entity.ToDTO<FoodAndDrink, FoodAndDrinkDTO>();
    }

    public async Task DeleteAsync(Guid id)
    {
        await _uow.FoodAndDrinkStore.DeleteAsync(id);
    }
}
