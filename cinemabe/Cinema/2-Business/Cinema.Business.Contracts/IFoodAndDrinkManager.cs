using Cinema.Business.DTO.Catalog;

namespace Cinema.Business.Contracts;

public interface IFoodAndDrinkManager : ICatalogManager<FoodAndDrinkDTO, CreateFoodAndDrinkRequest, UpdateFoodAndDrinkRequest> { }
