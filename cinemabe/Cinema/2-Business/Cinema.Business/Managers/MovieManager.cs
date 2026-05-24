using Cinema.Business.Contracts;
using Cinema.Business.DTO;
using Cinema.Business.DTO.Movies;
using Cinema.Business.DTO.Requests;
using Cinema.Business.Extensions;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;

namespace Cinema.Business.Managers;

public class MovieManager : IMovieManager
{
    private readonly IApplicationUnitOfWork _uow;

    public MovieManager(IApplicationUnitOfWork uow) => _uow = uow;

    public async Task<DefaultSearchResults<MovieDTO>> GetMoviesAsync(PagingSearchDTO search)
    {
        var searchText  = search.Filters.GetString("search");
        var movieTypeId = search.Filters.GetGuid("movieTypeId");
        var page        = search.PageIndex > 0 ? search.PageIndex : 1;
        var pageSize    = search.PageSize  > 0 ? search.PageSize  : 12;

        var (items, total) = await _uow.Movies.GetPagedAsync(searchText, movieTypeId, page, pageSize);
        var dtos = items.Select(ToMovieDTO).ToList();
        foreach (var dto in dtos)
            dto.AverageRating = await _uow.Movies.GetAverageRatingAsync(dto.Id);

        return new DefaultSearchResults<MovieDTO>
        {
            Results      = dtos,
            TotalCount   = total,
            CountPerPage = pageSize,
            Page         = page
        };
    }

    public async Task<DefaultSearchResults<MovieDTO>> GetNowShowingAsync(PagingSearchDTO search)
    {
        var movies = (await _uow.Movies.GetNowShowingAsync()).Select(ToMovieDTO).ToList();
        return Paginate(movies, search);
    }

    public async Task<DefaultSearchResults<MovieDTO>> GetComingSoonAsync(PagingSearchDTO search)
    {
        var movies = (await _uow.Movies.GetComingSoonAsync()).Select(ToMovieDTO).ToList();
        return Paginate(movies, search);
    }

    public async Task<DefaultSearchResults<ShowTimeListDTO>> GetShowTimesAsync(PagingSearchDTO search)
    {
        var movieId   = search.Filters.GetGuid("movieId")   ?? Guid.Empty;
        var theaterId = search.Filters.GetGuid("theaterId") ?? Guid.Empty;
        var date      = search.Filters.GetDateOnly("date")  ?? DateOnly.FromDateTime(DateTime.Today);

        var showTimes = await _uow.ShowTimes.GetByMovieAndDateAsync(movieId, theaterId, date);
        var dtos = showTimes.Select(s => new ShowTimeListDTO
        {
            Id             = s.Id,
            StartTime      = s.StartTime,
            EndTime        = s.EndTime,
            ProjectionForm = s.ProjectionForm,
            ShowTimeType   = s.ShowTimeType,
            Rooms = s.ShowTimeRooms.Select(sr => new ShowTimeRoomDTO
            {
                RoomId      = sr.RoomId,
                RoomName    = sr.Room?.Name,
                TheaterName = sr.Room?.Theater?.Name,
                BasePrice   = sr.BasePrice
            }).ToList()
        }).ToList();

        return new DefaultSearchResults<ShowTimeListDTO>
        {
            Results      = dtos,
            TotalCount   = dtos.Count,
            CountPerPage = dtos.Count,
            Page         = 1
        };
    }

    public async Task<MovieDetailDTO> GetDetailAsync(Guid id)
    {
        var movie = await _uow.Movies.GetDetailAsync(id)
                    ?? throw new KeyNotFoundException($"Movie {id} not found.");
        var dto = movie.ToDTO<Movie, MovieDetailDTO>();
        ApplyMovieComputedFields(movie, dto);
        dto.AgeRestrictionDescription = movie.AgeRestriction?.Description ?? string.Empty;
        dto.AgeRestrictionMinAge      = movie.AgeRestriction?.MinAge ?? 0;
        dto.AverageRating             = await _uow.Movies.GetAverageRatingAsync(id);
        dto.RatingCount               = movie.Evaluations.Count;
        dto.RecentComments = movie.Comments
            .Where(c => c.ParentId == null).Take(10)
            .Select(c =>
            {
                var cd = c.ToDTO<Comment, CommentDTO>();
                cd.UserName   = c.User?.Name ?? string.Empty;
                cd.UserAvatar = c.User?.Avatar;
                return cd;
            }).ToList();
        return dto;
    }

    public async Task<MovieDTO> CreateAsync(CreateMovieRequest request)
    {
        var movie = request.ToNewEntity<CreateMovieRequest, Movie>();
        movie.MovieTypeDetails = request.MovieTypeIds.Select(id => new MovieTypeDetail { MovieTypeId = id }).ToList();
        await _uow.Movies.CreateAsync(movie);
        return await GetBasicDTOAsync(movie.Id);
    }

    public async Task<MovieDTO> UpdateAsync(UpdateMovieRequest request)
    {
        var movie = await _uow.Movies.GetByIdAsync(request.Id)
                    ?? throw new KeyNotFoundException($"Movie {request.Id} not found.");
        movie.PatchEntity<Movie, UpdateMovieRequest>(request);
        movie.EndDate = request.EndDate;
        await _uow.Movies.UpdateAsync(movie);
        await _uow.SaveChangesAsync();
        return await GetBasicDTOAsync(request.Id);
    }

    public async Task DeleteAsync(Guid id)
    {
        var movie = await _uow.Movies.GetByIdAsync(id)
                    ?? throw new KeyNotFoundException($"Movie {id} not found.");
        movie.IsActive = false;
        await _uow.Movies.UpdateAsync(movie);
        await _uow.SaveChangesAsync();
    }

    public async Task<CommentDTO> AddCommentAsync(Guid movieId, Guid userId, string content, Guid? parentId)
    {
        var comment = new Comment { MovieId = movieId, UserId = userId, Content = content, ParentId = parentId };
        await _uow.Comments.CreateAsync(comment);
        return new CommentDTO { Id = comment.Id, Content = content, CreationTime = DateTime.UtcNow };
    }

    public async Task<int> RateMovieAsync(Guid movieId, Guid userId, int score, string? review)
    {
        var existing = (await _uow.Evaluations.FindAsync(e => e.MovieId == movieId && e.UserId == userId)).FirstOrDefault();
        if (existing != null)
        {
            existing.Score  = score;
            existing.Review = review;
            await _uow.Evaluations.UpdateAsync(existing);
        }
        else
        {
            await _uow.Evaluations.CreateAsync(new Evaluation { MovieId = movieId, UserId = userId, Score = score, Review = review });
        }
        await _uow.SaveChangesAsync();
        return score;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<MovieDTO> GetBasicDTOAsync(Guid id)
    {
        var movie = await _uow.Movies.GetDetailAsync(id) ?? throw new KeyNotFoundException();
        return ToMovieDTO(movie);
    }

    private static MovieDTO ToMovieDTO(Movie movie)
    {
        var dto = movie.ToDTO<Movie, MovieDTO>();
        ApplyMovieComputedFields(movie, dto);
        return dto;
    }

    private static void ApplyMovieComputedFields(Movie movie, MovieDTO dto)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        dto.AgeRestrictionCode = movie.AgeRestriction?.Code ?? string.Empty;
        dto.Genres             = movie.MovieTypeDetails?.Select(mt => mt.MovieType?.Name ?? string.Empty).ToList() ?? [];
        dto.IsNowShowing       = movie.ReleaseDate <= today && (movie.EndDate == null || movie.EndDate >= today);
        dto.IsComingSoon       = movie.ReleaseDate > today;
    }

    private static DefaultSearchResults<T> Paginate<T>(List<T> all, PagingSearchDTO search)
    {
        var page     = search.PageIndex > 0 ? search.PageIndex : 1;
        var pageSize = search.PageSize  > 0 ? search.PageSize  : all.Count;
        var paged    = pageSize > 0 ? all.Skip((page - 1) * pageSize).Take(pageSize).ToList() : all;
        return new DefaultSearchResults<T>
        {
            Results      = paged,
            TotalCount   = all.Count,
            CountPerPage = pageSize,
            Page         = page
        };
    }
}
