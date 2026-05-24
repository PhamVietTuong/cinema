using Cinema.Business.DTO.Requests;
using Cinema.Business.DTO.Theaters;
using Cinema.Data.Entities;
namespace Cinema.Business.Contracts;
public interface ITheaterManager
{
    Task<DefaultSearchResults<TheaterDTO>> GetTheatersAsync(PagingSearchDTO search);
    Task<DefaultSearchResults<TheaterDTO>> GetTheatersByMovieAsync(PagingSearchDTO search);
    Task<TheaterDTO>                       GetByIdAsync(Guid id);
    Task<TheaterDTO>                       CreateAsync(CreateTheaterRequest request);
    Task<TheaterDTO>                       UpdateAsync(UpdateTheaterRequest request);
    Task                                   DeleteAsync(Guid id);
}
