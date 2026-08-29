using Cinema.Business.DTO.Catalog;

namespace Cinema.Business.Contracts;

public interface IShowTimeManager : ICatalogManager<ShowTimeDTO, CreateShowTimeRequest, UpdateShowTimeRequest> { }
