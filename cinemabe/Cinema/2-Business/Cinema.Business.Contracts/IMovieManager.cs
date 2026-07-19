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
    /// <summary>Admin: paged comments for moderation. Filters: "approved" (bool), "movieId" (Guid).</summary>
    Task<DefaultSearchResults<CommentModerationDTO>> GetCommentsForModerationAsync(PagingSearchDTO search);
    /// <summary>Admin: approve or hide a comment. Hidden (unapproved) comments disappear from public listings.</summary>
    Task<bool>                                 ModerateCommentAsync(Guid commentId, bool approved);
    /// <summary>Admin: delete a comment and its direct replies. Returns false if not found.</summary>
    Task<bool>                                 DeleteCommentAsync(Guid commentId);
    Task<int>                                  RateMovieAsync(Guid movieId, Guid userId, int score, string? review);
    /// <summary>Recommends movies by the user's favourite genres (if signed in) then by rating.</summary>
    Task<List<MovieDTO>>                       GetRecommendedAsync(Guid? userId, int count);
}
