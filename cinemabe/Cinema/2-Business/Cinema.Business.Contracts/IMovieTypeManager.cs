using Cinema.Business.DTO.Catalog;

namespace Cinema.Business.Contracts;

public interface IMovieTypeManager : ICatalogManager<MovieTypeDTO, CreateMovieTypeRequest, UpdateMovieTypeRequest> { }
