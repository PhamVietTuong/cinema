using Cinema.Business.DTO.Movies;
using Cinema.Business.DTO.Requests;
using Cinema.Data.Entities;
namespace Cinema.Business.Contracts;
public interface IMovieManager
{
    Task<DefaultSearchResults<MovieDTO>>       GetMoviesAsync(PagingSearchDTO search);
    Task<DefaultSearchResults<MovieDTO>>       GetNowShowingAsync(PagingSearchDTO search);
    Task<DefaultSearchResults<MovieDTO>>       GetComingSoonAsync(PagingSearchDTO search);
    Task<DefaultSearchResults<ShowTimeListDTO>> GetShowTimesAsync(PagingSearchDTO search);
    Task<MovieDetailDTO>                       GetDetailAsync(Guid id);
    Task<MovieDTO>                             CreateAsync(CreateMovieRequest request);
    Task<MovieDTO>                             UpdateAsync(UpdateMovieRequest request);
    Task                                       DeleteAsync(Guid id);
    Task<CommentDTO>                           AddCommentAsync(Guid movieId, Guid userId, string content, Guid? parentId);
    Task<int>                                  RateMovieAsync(Guid movieId, Guid userId, int score, string? review);
}
