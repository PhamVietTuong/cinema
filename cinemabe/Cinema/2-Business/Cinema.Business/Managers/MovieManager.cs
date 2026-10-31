using Cinema.Business.Contracts;
using Cinema.Business.Contracts.Exceptions;
using Cinema.Business.DTO;
using Cinema.Business.DTO.Movies;
using Cinema.Business.DTO.Requests;
using Cinema.Business.Extensions;
using Cinema.Business.Helpers;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Cinema.Data.Enums;

namespace Cinema.Business.Managers;

public class MovieManager : IMovieManager
{
    private readonly IApplicationUnitOfWork _uow;
    private readonly IExternalRatingsProvider _externalRatings;

    /// <summary>The movie-detail page shows today + the next 3 days; its date tabs slice this window
    /// entirely client-side (no re-fetch per tab), so the response must cover exactly it.</summary>
    private const int ScheduleWindowDays = 4;

    private const string ReviewNotAllowedMessage = "You can only rate or comment on a movie after watching it.";

    public MovieManager(IApplicationUnitOfWork uow, IExternalRatingsProvider externalRatings)
    {
        _uow = uow;
        _externalRatings = externalRatings;
    }

    public async Task<DefaultSearchResults<MovieDTO>> GetMoviesAsync(PagingSearchDTO search)
    {
        var searchText  = search.Filters.GetString("search") ?? search.Filters.GetString("title");
        var director    = search.Filters.GetString("director");
        var movieTypeId = search.Filters.GetGuid("movieTypeId");
        var page        = search.PageIndex > 0 ? search.PageIndex : 1;
        var pageSize    = search.PageSize  > 0 ? search.PageSize  : 12;

        var (items, total) = await _uow.MovieStore.GetPagedAsync(searchText, director, movieTypeId, page, pageSize);
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
                RoomId       = sr.RoomId,
                RoomName     = sr.Room?.Name,
                RoomTypeName = sr.Room?.RoomType?.Name,
                TheaterName  = sr.Room?.Theater?.Name,
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

    public async Task<MovieDetailDTO> GetDetailAsync(Guid id, Guid? userId)
    {
        var movie = await _uow.MovieStore.GetDetailAsync(id);
        if (movie == null)
        {
            throw new KeyNotFoundException($"Movie {id} not found.");
        }
        return await BuildDetailAsync(movie, userId);
    }

    public async Task<MovieDetailDTO> GetDetailBySlugAsync(string slug, Guid? userId)
    {
        var normalizedSlug = slug?.Trim().ToLowerInvariant() ?? string.Empty;
        var movie = normalizedSlug.Length > 0 ? await _uow.MovieStore.GetDetailBySlugAsync(normalizedSlug) : null;
        if (movie == null)
        {
            throw new KeyNotFoundException($"Movie '{slug}' not found.");
        }
        return await BuildDetailAsync(movie, userId);
    }

    private async Task<MovieDetailDTO> BuildDetailAsync(Movie movie, Guid? userId)
    {
        var id = movie.Id;
        // Started first so it overlaps the DB queries below; the provider never touches the DbContext.
        var externalRatingsTask = _externalRatings.GetRatingsAsync(movie.Title, movie.ReleaseDate.Year, movie.ImdbId);
        var dto = movie.ToDTO<Movie, MovieDetailDTO>();
        ApplyMovieComputedFields(movie, dto);
        dto.AgeRestrictionDescription = movie.AgeRestriction?.Description ?? string.Empty;
        dto.AgeRestrictionMinAge      = movie.AgeRestriction?.MinAge ?? 0;
        // Bounded schedule window (today + next ScheduleWindowDays-1 more days): the detail page's
        // date tabs slice this client-side with no further requests, and "now" (not just "today")
        // excludes screenings that have already started — the detail page's showtimes are booking
        // links, so a screening that already started must not be clickable.
        var now  = DateTime.Now;
        var from = now;
        var to   = DateTime.Today.AddDays(ScheduleWindowDays);
        var scheduleRows = await _uow.ShowTimeStore.GetMovieScheduleAsync(id, from, to);

        // AvailableSeats = room capacity minus seats already booked (Pending/Paid) for that showtime.
        // Booked counts come from one grouped query for the whole movie — querying per showtime-room
        // turned a public page into 1 + N round-trips as a film's schedule grew.
        var bookedCounts = await _uow.SeatStore.GetBookedSeatCountsByMovieAsync(id);
        var summaries = scheduleRows.Select(row =>
        {
            var booked = bookedCounts.TryGetValue((row.ShowTimeId, row.RoomId), out var n) ? n : 0;
            return new ShowTimeSummaryDTO
            {
                Id             = row.ShowTimeId,
                StartTime      = row.StartTime,
                EndTime        = row.EndTime,
                ProjectionForm = row.ProjectionForm,
                RoomId         = row.RoomId,
                RoomName       = row.RoomName,
                RoomTypeName   = row.RoomTypeName,
                TheaterId      = row.TheaterId,
                TheaterName    = row.TheaterName,
                TheaterAddress = row.TheaterAddress,
                AvailableSeats = Math.Max(0, row.Capacity - booked),
            };
        }).ToList();
        dto.ShowTimes = summaries;
        dto.AverageRating             = await _uow.MovieStore.GetAverageRatingAsync(id);
        dto.RatingCount               = movie.Evaluations.Count;
        dto.RecentComments = (await _uow.CommentStore.GetRecentForMovieAsync(id, 10))
            .Select(ToCommentDTO).ToList();
        if (userId is Guid uid)
        {
            dto.CanReview = await CanReviewAsync(id, uid);
        }
        dto.Credits = (await _uow.MovieCreditStore.GetByMovieAsync(id)).Select(ToCreditDTO).ToList();
        dto.ExternalRatings = await externalRatingsTask;
        return dto;
    }

    public async Task<MovieDTO> CreateAsync(CreateMovieRequest request)
    {
        var movie = request.ToNewEntity<CreateMovieRequest, Movie>();
        movie.Slug = await GenerateUniqueSlugAsync(request.Title);
        movie.ImdbId = NormalizeImdbId(request.ImdbId);
        movie.MovieTypeDetails = request.MovieTypeIds.Select(id => new MovieTypeDetail { MovieTypeId = id }).ToList();
        if (request.Credits is { Count: > 0 })
        {
            var normalized = await NormalizeCreditsAsync(request.Credits);
            movie.MovieCredits = normalized.Select(c => new MovieCredit
            {
                PersonId      = c.PersonId,
                CreditType    = c.CreditType,
                CharacterName = c.CharacterName,
                DisplayOrder  = c.DisplayOrder,
            }).ToList();
        }
        // Any Person created or edited by NormalizeCreditsAsync (staged via PersonStore.TrackNew, or
        // mutated on an already-tracked/loaded entity) commits here, atomically with the movie.
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

        // Validate before mutating anything, so a bad request leaves the movie untouched.
        var normalizedCredits = request.Credits != null ? await NormalizeCreditsAsync(request.Credits) : null;

        // Slug is deliberately not patched here: it is generated once at creation and stays stable
        // across title edits so shared links and bookmarks keep working.
        movie.PatchEntity<Movie, UpdateMovieRequest>(request);
        movie.EndDate = request.EndDate;
        movie.ImdbId = NormalizeImdbId(request.ImdbId);

        // Reconcile genre links against the requested set on the tracked collection.
        movie.MovieTypeDetails.Clear();
        foreach (var typeId in request.MovieTypeIds.Distinct())
        {
            movie.MovieTypeDetails.Add(new MovieTypeDetail { MovieId = movie.Id, MovieTypeId = typeId });
        }

        if (normalizedCredits != null)
        {
            SyncCredits(movie, normalizedCredits, _uow.MovieCreditStore);
        }

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
        await EnsureCanReviewAsync(movieId, userId);
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
        await EnsureCanReviewAsync(movieId, userId);
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

    /// <summary>A credit row after validation/resolution: PersonId is always a real, resolved Guid.</summary>
    private sealed record NormalizedCredit(Guid PersonId, CreditType CreditType, string? CharacterName, int DisplayOrder);

    /// <summary>Validates and resolves a requested credit list. Every row always carries a PersonName
    /// (and optional PersonPhotoUrl): when PersonId is set, these edit that Person in place (applies to
    /// every movie they're credited on); when absent, they match-or-create by name. Phases 1-5 are
    /// read-only; phase 6 is the only mutation, reached only once every row has validated.
    /// 1. Per row, no DB access: a defined CreditType, a non-blank PersonName, and an explicit-id
    ///    (PersonId, CreditType) duplicate check — so a request using only explicit picks never
    ///    touches PersonStore.
    /// 2. One query loads every Person referenced by an explicit PersonId or by any row's PersonName
    ///    (tracked — a row with an id may edit that Person).
    /// 3. Every explicit PersonId must resolve to a loaded Person.
    /// 4. Computes the desired (Name, PhotoUrl) per explicit id (rows disagreeing about the same
    ///    person are rejected), then validates each resulting rename against every other loaded
    ///    person's current name and every other rename in this request — this also rejects a two-way
    ///    name swap, since each side's target name is still "currently taken" by the other.
    /// 5. Resolves every row, in request order, to a real PersonId: explicit; a name matching a phase-4
    ///    rename target; an existing Person matched by name (a non-blank photo here replaces theirs,
    ///    unless that person already has an explicit edit in this request, which wins); or a brand-new,
    ///    not-yet-saved Person (first non-blank photo among same-named new rows wins) — then checks the
    ///    resulting (PersonId, CreditType) pair for duplicates.
    /// 6. Applies every Person edit and stages every new Person via PersonStore.TrackNew.
    /// Throws InvalidOperationException (-> 400) on any violation.</summary>
    private async Task<List<NormalizedCredit>> NormalizeCreditsAsync(List<MovieCreditRequest> credits)
    {
        if (credits.Count == 0)
        {
            return new List<NormalizedCredit>();
        }

        var trimmedNames = new string[credits.Count];
        var explicitIds = new List<Guid>();
        var allNames = new List<string>();
        var seenKeys = new HashSet<(Guid PersonId, CreditType CreditType)>();

        for (var i = 0; i < credits.Count; i++)
        {
            var c = credits[i];
            if (!Enum.IsDefined(c.CreditType))
            {
                throw new InvalidOperationException("Invalid credit type.");
            }

            var name = c.PersonName.Trim();
            if (name.Length == 0)
            {
                throw new InvalidOperationException("Each credit must name a person.");
            }
            trimmedNames[i] = name;
            allNames.Add(name);

            if (c.PersonId is { } id && id != Guid.Empty)
            {
                explicitIds.Add(id);
                if (!seenKeys.Add((id, c.CreditType)))
                {
                    throw new InvalidOperationException("The same person cannot be credited twice in the same role.");
                }
            }
        }

        var distinctExplicitIds = explicitIds.Distinct().ToList();
        var distinctNames = allNames.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        var loaded = distinctExplicitIds.Count > 0 || distinctNames.Count > 0
            ? (await _uow.PersonStore.FindAllAsync(p => distinctExplicitIds.Contains(p.Id) || distinctNames.Contains(p.Name))).ToList()
            : new List<Person>();

        var byId = loaded.ToDictionary(p => p.Id);
        var byName = new Dictionary<string, Person>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in loaded)
        {
            byName[p.Name] = p;
        }

        if (distinctExplicitIds.Any(id => !byId.ContainsKey(id)))
        {
            throw new InvalidOperationException("One or more credited people no longer exist.");
        }

        // Phase 4: the desired Name/PhotoUrl per explicit id, and the renames that result.
        var desiredById = new Dictionary<Guid, (string Name, string? Photo)>();
        for (var i = 0; i < credits.Count; i++)
        {
            if (credits[i].PersonId is not { } id || id == Guid.Empty)
            {
                continue;
            }
            var desired = (Name: trimmedNames[i], Photo: NormalizePhotoUrl(credits[i].PersonPhotoUrl));
            if (desiredById.TryGetValue(id, out var already) &&
                (!string.Equals(already.Name, desired.Name, StringComparison.Ordinal) || already.Photo != desired.Photo))
            {
                throw new InvalidOperationException("Conflicting name/photo for the same person in this save.");
            }
            desiredById[id] = desired;
        }

        var renameTargets = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var (id, desired) in desiredById)
        {
            if (string.Equals(byId[id].Name, desired.Name, StringComparison.Ordinal))
            {
                continue;
            }
            if (byName.TryGetValue(desired.Name, out var clash) && clash.Id != id)
            {
                throw new InvalidOperationException("A person with this name already exists.");
            }
            if (renameTargets.TryGetValue(desired.Name, out var claimedBy) && claimedBy != id)
            {
                throw new InvalidOperationException("A person with this name already exists.");
            }
            renameTargets[desired.Name] = id;
        }

        // Phase 5: resolve every row to a real PersonId, in request order.
        var pendingByName = new Dictionary<string, Person>(StringComparer.OrdinalIgnoreCase);
        var matchedPhotoById = new Dictionary<Guid, string?>();
        var normalized = new List<NormalizedCredit>(credits.Count);

        for (var i = 0; i < credits.Count; i++)
        {
            var c = credits[i];
            var name = trimmedNames[i];
            var hasId = c.PersonId is { } pid && pid != Guid.Empty;
            Guid personId;

            if (hasId)
            {
                personId = c.PersonId!.Value;
            }
            else if (renameTargets.TryGetValue(name, out var renamedId))
            {
                personId = renamedId;
            }
            else if (byName.TryGetValue(name, out var existing))
            {
                if (desiredById.TryGetValue(existing.Id, out var existingDesired) &&
                    !string.Equals(existingDesired.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"'{name}' conflicts with a person being renamed in this save.");
                }
                personId = existing.Id;
                // An explicit edit elsewhere in this request already governs this person's final
                // photo (applied in phase 6); only track a name-matched photo when there isn't one.
                var photo = NormalizePhotoUrl(c.PersonPhotoUrl);
                if (photo != null && !desiredById.ContainsKey(existing.Id))
                {
                    if (matchedPhotoById.TryGetValue(existing.Id, out var already) && already != photo)
                    {
                        throw new InvalidOperationException("Conflicting name/photo for the same person in this save.");
                    }
                    matchedPhotoById[existing.Id] = photo;
                }
            }
            else if (pendingByName.TryGetValue(name, out var pending))
            {
                personId = pending.Id;
                var photo = NormalizePhotoUrl(c.PersonPhotoUrl);
                if (pending.PhotoUrl == null && photo != null)
                {
                    pending.PhotoUrl = photo;
                }
            }
            else
            {
                var created = new Person { Name = name, PhotoUrl = NormalizePhotoUrl(c.PersonPhotoUrl) };
                pendingByName[name] = created;
                personId = created.Id;
            }

            if (!hasId && !seenKeys.Add((personId, c.CreditType)))
            {
                throw new InvalidOperationException("The same person cannot be credited twice in the same role.");
            }

            var characterName = c.CreditType == CreditType.Director ? null : NormalizeCharacterName(c.CharacterName);
            normalized.Add(new NormalizedCredit(personId, c.CreditType, characterName, c.DisplayOrder));
        }

        // Phase 6: the only mutation, reached only once every row above has validated.
        foreach (var (id, desired) in desiredById)
        {
            var person = byId[id];
            if (!string.Equals(person.Name, desired.Name, StringComparison.Ordinal) || person.PhotoUrl != desired.Photo)
            {
                person.Name = desired.Name;
                person.PhotoUrl = desired.Photo;
                person.LastUpdatedTime = DateTime.UtcNow;
            }
        }
        foreach (var (id, photo) in matchedPhotoById)
        {
            var person = byId[id];
            if (person.PhotoUrl != photo)
            {
                person.PhotoUrl = photo;
                person.LastUpdatedTime = DateTime.UtcNow;
            }
        }
        foreach (var person in pendingByName.Values)
        {
            _uow.PersonStore.TrackNew(person);
        }

        return normalized;
    }

    private static string? NormalizeCharacterName(string? characterName)
    {
        return string.IsNullOrWhiteSpace(characterName) ? null : characterName.Trim();
    }

    private static string? NormalizePhotoUrl(string? photoUrl)
    {
        return string.IsNullOrWhiteSpace(photoUrl) ? null : photoUrl.Trim();
    }

    private static string? NormalizeImdbId(string? imdbId)
    {
        return string.IsNullOrWhiteSpace(imdbId) ? null : imdbId.Trim();
    }

    /// <summary>Reconciles the movie's tracked MovieCredits collection against the requested list by
    /// key (PersonId, CreditType): updates matches in place, adds new rows, removes rows no longer
    /// requested. Keeps Ids stable instead of clearing and re-adding everything.</summary>
    private static void SyncCredits(Movie movie, List<NormalizedCredit> requested, IMovieCreditStore creditStore)
    {
        var existingByKey = movie.MovieCredits.ToDictionary(c => (c.PersonId, c.CreditType));
        var requestedKeys = new HashSet<(Guid PersonId, CreditType CreditType)>();

        foreach (var r in requested)
        {
            var key = (r.PersonId, r.CreditType);
            requestedKeys.Add(key);
            if (existingByKey.TryGetValue(key, out var existing))
            {
                existing.CharacterName   = r.CharacterName;
                existing.DisplayOrder    = r.DisplayOrder;
                existing.LastUpdatedTime = DateTime.UtcNow;
            }
            else
            {
                // Appending to the tracked Movie's collection isn't enough here: BaseEntity.Id already
                // has a non-default Guid by the time EF sees this object, so the change tracker's
                // fixup would mark it Modified (-> a no-op UPDATE) instead of Added. TrackNew makes
                // the Add explicit.
                creditStore.TrackNew(new MovieCredit
                {
                    MovieId       = movie.Id,
                    PersonId      = r.PersonId,
                    CreditType    = r.CreditType,
                    CharacterName = r.CharacterName,
                    DisplayOrder  = r.DisplayOrder,
                });
            }
        }

        foreach (var stale in existingByKey.Where(kv => !requestedKeys.Contains(kv.Key)).Select(kv => kv.Value).ToList())
        {
            movie.MovieCredits.Remove(stale);
        }
    }

    private static MovieCreditDTO ToCreditDTO(MovieCreditRow r)
    {
        return new MovieCreditDTO
        {
            Id              = r.Id,
            PersonId        = r.PersonId,
            PersonName      = r.PersonName,
            PersonPhotoUrl  = r.PersonPhotoUrl,
            CreditType      = r.CreditType,
            CharacterName   = r.CharacterName,
            DisplayOrder    = r.DisplayOrder,
        };
    }

    private async Task<string> GenerateUniqueSlugAsync(string title)
    {
        var baseSlug = SlugHelper.ToBaseSlug(title);
        var taken = await _uow.MovieStore.GetSlugsStartingWithAsync(baseSlug);
        return SlugHelper.PickUnique(baseSlug, taken);
    }

    private Task<bool> CanReviewAsync(Guid movieId, Guid userId)
    {
        return _uow.InvoiceStore.HasWatchedMovieAsync(userId, movieId, DateTime.Now);
    }

    private async Task EnsureCanReviewAsync(Guid movieId, Guid userId)
    {
        if (!await CanReviewAsync(movieId, userId))
        {
            throw new AccessDeniedException(ReviewNotAllowedMessage);
        }
    }

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
