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

    public MovieManager(IApplicationUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<DefaultSearchResults<MovieDTO>> GetMoviesAsync(PagingSearchDTO search)
    {
        var searchText  = search.Filters.GetString("search");
        var movieTypeId = search.Filters.GetGuid("movieTypeId");
        var page        = search.PageIndex > 0 ? search.PageIndex : 1;
        var pageSize    = search.PageSize  > 0 ? search.PageSize  : 12;

        var (items, total) = await _uow.MovieStore.GetPagedAsync(searchText, movieTypeId, page, pageSize);
        var dtos = items.Select(ToMovieDTO).ToList();

        // One grouped query for the whole page. Querying per movie made this endpoint issue
        // 1 + N queries, and GetRecommendedAsync calls it with a 200-row page.
        var ratings = await _uow.MovieStore.GetAverageRatingsAsync(dtos.Select(d => d.Id).ToList());
        foreach (var dto in dtos)
        {
            dto.AverageRating = ratings.TryGetValue(dto.Id, out var avg) ? avg : 0;
        }

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
        var movies = (await _uow.MovieStore.GetNowShowingAsync()).Select(ToMovieDTO).ToList();
        return Paginate(movies, search);
    }

    public async Task<DefaultSearchResults<MovieDTO>> GetComingSoonAsync(PagingSearchDTO search)
    {
        var movies = (await _uow.MovieStore.GetComingSoonAsync()).Select(ToMovieDTO).ToList();
        return Paginate(movies, search);
    }

    public async Task<DefaultSearchResults<ShowTimeListDTO>> GetShowTimesAsync(PagingSearchDTO search)
    {
        var movieId   = search.Filters.GetGuid("movieId")   ?? Guid.Empty;
        var theaterId = search.Filters.GetGuid("theaterId") ?? Guid.Empty;
        var date      = search.Filters.GetDateOnly("date")  ?? DateOnly.FromDateTime(DateTime.Today);

        var showTimes = await _uow.ShowTimeStore.GetByMovieAndDateAsync(movieId, theaterId, date);
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
        var movie = await _uow.MovieStore.GetDetailAsync(id);
        if (movie == null)
        {
            throw new KeyNotFoundException($"Movie {id} not found.");
        }
        var dto = movie.ToDTO<Movie, MovieDetailDTO>();
        ApplyMovieComputedFields(movie, dto);
        dto.AgeRestrictionDescription = movie.AgeRestriction?.Description ?? string.Empty;
        dto.AgeRestrictionMinAge      = movie.AgeRestriction?.MinAge ?? 0;
        // Flatten each showtime's rooms into the summary list the detail page renders.
        // AvailableSeats = room capacity minus seats already booked (Pending/Paid) for that showtime.
        // Booked counts come from one grouped query for the whole movie — querying per showtime-room
        // turned a public page into 1 + N round-trips as a film's schedule grew.
        var bookedCounts = await _uow.SeatStore.GetBookedSeatCountsByMovieAsync(id);
        var summaries = new List<ShowTimeSummaryDTO>();
        // Only screenings that haven't started yet: the detail page's showtimes are booking links,
        // and it previously listed past ones (labelled with a time but no date) right alongside
        // upcoming ones, so a customer could click through and book a screening that had ended.
        var now = DateTime.Now;
        foreach (var s in movie.ShowTimes.Where(s => s.IsActive && s.StartTime > now))
        {
            foreach (var sr in s.ShowTimeRooms.DefaultIfEmpty())
            {
                var capacity = (sr?.Room?.TotalRows ?? 0) * (sr?.Room?.TotalColumns ?? 0);
                var booked   = sr != null && bookedCounts.TryGetValue((s.Id, sr.RoomId), out var n) ? n : 0;
                summaries.Add(new ShowTimeSummaryDTO
                {
                    Id             = s.Id,
                    StartTime      = s.StartTime,
                    EndTime        = s.EndTime,
                    ProjectionForm = s.ProjectionForm,
                    RoomId         = sr?.RoomId ?? Guid.Empty,
                    RoomName       = sr?.Room?.Name ?? string.Empty,
                    TheaterName    = sr?.Room?.Theater?.Name ?? string.Empty,
                    AvailableSeats = Math.Max(0, capacity - booked),
                });
            }
        }
        dto.ShowTimes = summaries.OrderBy(x => x.StartTime).ToList();
        dto.AverageRating             = await _uow.MovieStore.GetAverageRatingAsync(id);
        dto.RatingCount               = movie.Evaluations.Count;
        dto.RecentComments = (await _uow.CommentStore.GetRecentForMovieAsync(id, 10))
            .Select(ToCommentDTO).ToList();
        return dto;
    }

    public async Task<MovieDTO> CreateAsync(CreateMovieRequest request)
    {
        var movie = request.ToNewEntity<CreateMovieRequest, Movie>();
        movie.MovieTypeDetails = request.MovieTypeIds.Select(id => new MovieTypeDetail { MovieTypeId = id }).ToList();
        await _uow.MovieStore.CreateAsync(movie);
        return await GetBasicDTOAsync(movie.Id);
    }

    public async Task<MovieDTO> UpdateAsync(UpdateMovieRequest request)
    {
        var movie = await _uow.MovieStore.GetForUpdateAsync(request.Id);
        if (movie == null)
        {
            throw new KeyNotFoundException($"Movie {request.Id} not found.");
        }
        movie.PatchEntity<Movie, UpdateMovieRequest>(request);
        movie.EndDate = request.EndDate;

        // Reconcile genre links against the requested set on the tracked collection.
        movie.MovieTypeDetails.Clear();
        foreach (var typeId in request.MovieTypeIds.Distinct())
            movie.MovieTypeDetails.Add(new MovieTypeDetail { MovieId = movie.Id, MovieTypeId = typeId });

        movie.LastUpdatedTime = DateTime.UtcNow;
        await _uow.SaveChangesAsync();
        return await GetBasicDTOAsync(request.Id);
    }

    public async Task DeleteAsync(Guid id)
    {
        var movie = await _uow.MovieStore.GetByIdAsync(id);
        if (movie == null)
        {
            throw new KeyNotFoundException($"Movie {id} not found.");
        }
        movie.IsActive = false;
        await _uow.MovieStore.UpdateAsync(movie);
        await _uow.SaveChangesAsync();
    }

    public async Task<CommentDTO> AddCommentAsync(Guid movieId, Guid userId, string content, Guid? parentId)
    {
        var comment = new Comment { MovieId = movieId, UserId = userId, Content = content, ParentId = parentId };
        await _uow.CommentStore.CreateAsync(comment);
        return new CommentDTO { Id = comment.Id, Content = content, CreationTime = DateTime.UtcNow };
    }

    public async Task<DefaultSearchResults<CommentModerationDTO>> GetCommentsForModerationAsync(PagingSearchDTO search)
    {
        var approved = search.Filters.GetBool("approved");
        var movieId  = search.Filters.GetGuid("movieId");
        var page     = search.PageIndex > 0 ? search.PageIndex : 1;
        var size     = search.PageSize  > 0 ? search.PageSize  : 20;

        var (items, total) = await _uow.CommentStore.GetForModerationAsync(approved, movieId, page, size);
        return new DefaultSearchResults<CommentModerationDTO>
        {
            Results = items.Select(c => new CommentModerationDTO
            {
                Id           = c.Id,
                MovieId      = c.MovieId,
                MovieTitle   = c.Movie?.Title ?? string.Empty,
                UserName     = c.User?.Name ?? string.Empty,
                Content      = c.Content,
                IsApproved   = c.IsApproved,
                ParentId     = c.ParentId,
                CreationTime = c.CreationTime,
            }).ToList(),
            TotalCount   = total,
            Page         = page,
            CountPerPage = size,
        };
    }

    public async Task<bool> ModerateCommentAsync(Guid commentId, bool approved)
    {
        var comment = await _uow.CommentStore.GetByIdAsync(commentId);
        if (comment is null)
        {
            return false;
        }
        comment.IsApproved = approved;
        await _uow.CommentStore.UpdateAsync(comment);
        await _uow.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteCommentAsync(Guid commentId)
    {
        var comment = await _uow.CommentStore.GetByIdAsync(commentId);
        if (comment is null)
        {
            return false;
        }
        // Remove direct replies too so none are left orphaned (moderation is a hard removal of content).
        // Each store delete commits on its own, so without an explicit transaction a failure partway
        // through left some replies deleted and the parent still standing.
        var replies = await _uow.CommentStore.GetRepliesAsync(commentId);
        await _uow.BeginTransactionAsync();
        try
        {
            foreach (var reply in replies)
            {
                await _uow.CommentStore.DeleteAsync(reply);
            }
            await _uow.CommentStore.DeleteAsync(comment);
            await _uow.SaveChangesAsync();
            await _uow.CommitTransactionAsync();
        }
        catch
        {
            await _uow.RollbackTransactionAsync();
            throw;
        }
        return true;
    }

    public async Task<int> RateMovieAsync(Guid movieId, Guid userId, int score, string? review)
    {
        var existing = (await _uow.EvaluationStore.FindAsync(e => e.MovieId == movieId && e.UserId == userId)).FirstOrDefault();
        if (existing != null)
        {
            existing.Score  = score;
            existing.Review = review;
            await _uow.EvaluationStore.UpdateAsync(existing);
        }
        else
        {
            await _uow.EvaluationStore.CreateAsync(new Evaluation { MovieId = movieId, UserId = userId, Score = score, Review = review });
        }
        await _uow.SaveChangesAsync();
        return score;
    }

    public async Task<List<MovieDTO>> GetRecommendedAsync(Guid? userId, int count)
    {
        // Candidate pool: active movies (GetMoviesAsync already fills rating + genres).
        var pool = (await GetMoviesAsync(new PagingSearchDTO { PageIndex = 1, PageSize = 200, Filters = new() }))
            .Results.Where(m => m.IsActive).ToList();

        var ratedIds  = new HashSet<Guid>();
        var favGenres = new HashSet<string>();
        if (userId is Guid uid)
        {
            var evals = (await _uow.EvaluationStore.FindAsync(e => e.UserId == uid)).ToList();
            ratedIds  = evals.Select(e => e.MovieId).ToHashSet();
            var liked = evals.Where(e => e.Score >= 4).Select(e => e.MovieId).ToHashSet();
            favGenres = pool.Where(m => liked.Contains(m.Id)).SelectMany(m => m.Genres).ToHashSet();
        }

        // Movies in a favourite genre first, then highest-rated; exclude ones already rated.
        return pool
            .Where(m => !ratedIds.Contains(m.Id))
            .OrderByDescending(m => m.Genres.Any(g => favGenres.Contains(g)) ? 1 : 0)
            .ThenByDescending(m => m.AverageRating)
            .ThenByDescending(m => m.RatingCount)
            .Take(count > 0 ? count : 8)
            .ToList();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Maps a comment (and its approved replies, recursively) to a DTO.</summary>
    private static CommentDTO ToCommentDTO(CommentView c)
    {
        return new()
        {
            Id           = c.Id,
            Content      = c.Content,
            ParentId     = c.ParentId,
            CreationTime = c.CreationTime,
            UserName     = c.UserName,
            UserAvatar   = c.UserAvatar,
            Replies      = c.Replies.Select(ToCommentDTO).ToList(),
        };
    }

    private async Task<MovieDTO> GetBasicDTOAsync(Guid id)
    {
        var movie = await _uow.MovieStore.GetDetailAsync(id);
        if (movie == null)
        {
            throw new KeyNotFoundException();
        }
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
        dto.MovieTypeIds       = movie.MovieTypeDetails?.Select(mt => mt.MovieTypeId).ToList() ?? [];
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
