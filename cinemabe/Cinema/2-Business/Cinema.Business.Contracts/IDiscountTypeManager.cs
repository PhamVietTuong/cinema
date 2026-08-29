using Cinema.Business.DTO.Catalog;

namespace Cinema.Business.Contracts;

public interface IDiscountTypeManager : ICatalogManager<DiscountTypeDTO, CreateDiscountTypeRequest, UpdateDiscountTypeRequest> { }
