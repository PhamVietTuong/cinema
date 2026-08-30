using Cinema.Business.DTO.Catalog;
using Cinema.Business.DTO.Requests;
using Cinema.Data.Entities;

namespace Cinema.Business.Contracts;

public interface IFoodAndDrinkManager
{
    Task<DefaultSearchResults<FoodAndDrinkDTO>> GetAsync(PagingSearchDTO search);
    Task<bool>                                  ExistsAsync(Guid id);
    Task<FoodAndDrinkDTO>                       GetByIdAsync(Guid id);
    Task<FoodAndDrinkDTO>                       CreateAsync(CreateFoodAndDrinkRequest request);
    Task<FoodAndDrinkDTO>                       UpdateAsync(UpdateFoodAndDrinkRequest request);
    Task                                        DeleteAsync(Guid id);
}
