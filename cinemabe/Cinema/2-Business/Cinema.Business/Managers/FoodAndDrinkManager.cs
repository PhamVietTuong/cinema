using System.Linq.Expressions;
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

    public async Task<DefaultSearchResults<FoodAndDrinkDTO>> GetAsync(PagingSearchDTO search)
    {
        search ??= new PagingSearchDTO();
        var (page, pageSize) = PagingHelper.ResolvePaging(search);
        var keyword = search.Filters.GetString("keyword");
        var theaterId = search.Filters.GetGuid("theaterId");
        var isAvailable = search.Filters.GetBool("isAvailable");

        Expression<Func<FoodAndDrink, bool>> predicate = e =>
            (string.IsNullOrEmpty(keyword) || e.Name.Contains(keyword!)) &&
            (theaterId == null || e.TheaterId == theaterId) &&
            (isAvailable == null || e.IsAvailable == isAvailable);

        var total = await _uow.FoodAndDrinkStore.CountAsync(predicate);
        var items = await _uow.FoodAndDrinkStore.FindAllPageAsync(page - 1, pageSize, predicate);
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
