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
    Task<MovieDetailDTO>                       GetDetailAsync(Guid id, Guid? userId);
    Task<MovieDetailDTO>                       GetDetailBySlugAsync(string slug, Guid? userId);
    Task<MovieDTO>                             CreateAsync(CreateMovieRequest request);
    Task<MovieDTO>                             UpdateAsync(UpdateMovieRequest request);
    Task                                       DeleteAsync(Guid id);
    /// <summary>Throws <see cref="Cinema.Business.Contracts.Exceptions.AccessDeniedException"/> when the user has not watched the movie.</summary>
    Task<CommentDTO>                           AddCommentAsync(Guid movieId, Guid userId, string content, Guid? parentId);
    /// <summary>Paged approved top-level comments (with replies) of a movie. <paramref name="userId"/> only sets IsMine.</summary>
    Task<DefaultSearchResults<CommentDTO>>     GetCommentsAsync(Guid movieId, Guid? userId, PagingSearchDTO search);
    /// <summary>Author-only edit. Throws AccessDeniedException for a non-author, KeyNotFoundException when missing.</summary>
    Task<CommentDTO>                           UpdateOwnCommentAsync(Guid commentId, Guid userId, string content);
    /// <summary>Author-only delete (replies go with it). Throws AccessDeniedException for a non-author, KeyNotFoundException when missing.</summary>
    Task                                       DeleteOwnCommentAsync(Guid commentId, Guid userId);
    /// <summary>Public cross-movie showtime search. Filters: movieId, theaterId, movieTypeId, language, date (yyyy-MM-dd), timeFrom/timeTo (HH:mm).</summary>
    Task<DefaultSearchResults<ShowTimeSearchResultDTO>> SearchShowTimesAsync(PagingSearchDTO search);
    /// <summary>Distinct languages of active movies, for the language filter.</summary>
    Task<List<string>>                         GetMovieLanguagesAsync();
    /// <summary>Admin: paged comments for moderation. Filters: "approved" (bool), "movieId" (Guid).</summary>
    Task<DefaultSearchResults<CommentModerationDTO>> GetCommentsForModerationAsync(PagingSearchDTO search);
    /// <summary>Admin: approve or hide a comment. Hidden (unapproved) comments disappear from public listings.</summary>
    Task<bool>                                 ModerateCommentAsync(Guid commentId, bool approved);
    /// <summary>Admin: delete a comment and its direct replies. Returns false if not found.</summary>
    Task<bool>                                 DeleteCommentAsync(Guid commentId);
    /// <summary>Throws <see cref="Cinema.Business.Contracts.Exceptions.AccessDeniedException"/> when the user has not watched the movie.</summary>
    /// <summary>Score must be 1-10 (InvalidOperationException otherwise). A null <paramref name="review"/> keeps the saved review; a blank one clears it.</summary>
    Task<int>                                  RateMovieAsync(Guid movieId, Guid userId, int score, string? review);
    /// <summary>Recommends movies by the user's favourite genres (if signed in) then by rating.</summary>
    Task<List<MovieDTO>>                       GetRecommendedAsync(Guid? userId, int count);
}
