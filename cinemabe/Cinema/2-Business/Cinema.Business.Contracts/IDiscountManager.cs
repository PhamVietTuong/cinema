using Cinema.Business.DTO.Catalog;

namespace Cinema.Business.Contracts;

public interface IDiscountManager : ICatalogManager<DiscountDTO, CreateDiscountRequest, UpdateDiscountRequest> { }
