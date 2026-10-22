using Cinema.Business.DTO.Catalog;
using Cinema.Business.DTO.Requests;
using Cinema.Data.Entities;

namespace Cinema.Business.Contracts;

/// <summary>CRUD for the Movie ↔ MovieType join table (composite key, no surrogate Id).</summary>
public interface IMovieTypeDetailManager
{
    Task<DefaultSearchResults<MovieTypeDetailDTO>> GetAsync(PagingSearchDTO search);
    Task<MovieTypeDetailDTO> CreateAsync(CreateMovieTypeDetailRequest request);
    Task DeleteAsync(Guid movieId, Guid movieTypeId);
}
