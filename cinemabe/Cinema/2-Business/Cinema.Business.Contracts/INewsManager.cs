using Cinema.Business.DTO.Catalog;

namespace Cinema.Business.Contracts;

public interface INewsManager : ICatalogManager<NewsDTO, CreateNewsRequest, UpdateNewsRequest> { }
