using Cinema.Business.DTO.Catalog;
using Cinema.Business.DTO.Requests;
using Cinema.Data.Entities;

namespace Cinema.Business.Contracts;

public interface IMovieTypeManager
{
    Task<DefaultSearchResults<MovieTypeDTO>> GetAsync(PagingSearchDTO search);
    Task<bool>                               ExistsAsync(Guid id);
    Task<MovieTypeDTO>                       GetByIdAsync(Guid id);
    Task<MovieTypeDTO>                       CreateAsync(CreateMovieTypeRequest request);
    Task<MovieTypeDTO>                       UpdateAsync(UpdateMovieTypeRequest request);
    Task                                     DeleteAsync(Guid id);
}
