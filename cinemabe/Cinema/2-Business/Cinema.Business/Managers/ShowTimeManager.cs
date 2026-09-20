using Cinema.Business.Contracts;
using Cinema.Business.DTO.Catalog;
using Cinema.Business.DTO.Requests;
using Cinema.Business.Extensions;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Cinema.Data.Enums;

namespace Cinema.Business.Managers;

public class ShowTimeManager : IShowTimeManager
{
    protected readonly IApplicationUnitOfWork _uow;

    public ShowTimeManager(IApplicationUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await _uow.ShowTimeStore.ExistsAsync(e => e.Id == id);
    }

    public async Task<DefaultSearchResults<ShowTimeDTO>> GetAsync(PagingSearchDTO search)
    {
        search ??= new PagingSearchDTO();
        var page = search.PageIndex > 0 ? search.PageIndex : 1;
        var pageSize = search.PageSize > 0 ? search.PageSize : 20;

        var (items, total) = await _uow.ShowTimeStore.SearchAsync(
            search.Filters.GetGuid("movieId"),
            search.Filters.GetGuid("roomId"),
            search.Filters.GetBool("isActive"),
            search.Filters.GetDateTime("from"),
            search.Filters.GetDateTime("to"),
            page, pageSize);

        return new DefaultSearchResults<ShowTimeDTO>
        {
            Results = items.Select(ToShowTimeDTO).ToList(),
            TotalCount = total,
            CountPerPage = pageSize,
            Page = page
        };
    }

    public async Task<ShowTimeDTO> GetByIdAsync(Guid id)
    {
        var entity = await _uow.ShowTimeStore.GetByIdWithRoomsAsync(id);
        if (entity == null)
        {
            throw new KeyNotFoundException($"ShowTime {id} not found.");
        }
        return ToShowTimeDTO(entity);
    }

    public async Task<ShowTimeDTO> CreateAsync(CreateShowTimeRequest request)
    {
        ValidateWindow(request.StartTime, request.EndTime, mustBeFuture: true);
        var movie = await LoadMovieRuntimeAsync(request.MovieId);
        ValidateAgainstRuntime(request.StartTime, request.EndTime, movie);
        ValidateMovieAvailability(request.StartTime, movie);

        var entity = request.ToNewEntity<CreateShowTimeRequest, ShowTime>();
        if (request.RoomId != Guid.Empty)
        {
            var roomClass = await LoadRoomClassAsync(request.RoomId);
            ValidateRoomAvailability(roomClass);
            ValidateRoomSupportsFormat(roomClass, entity.ProjectionForm);
            if (await _uow.ShowTimeStore.HasRoomOverlapAsync(request.RoomId, entity.StartTime, entity.EndTime, roomClass.TurnoverBufferMinutes, null))
            {
                throw new InvalidOperationException(RoomOverlapMessage(roomClass.TurnoverBufferMinutes));
            }
            entity.ShowTimeRooms.Add(new ShowTimeRoom { RoomId = request.RoomId, BasePrice = request.BasePrice });
        }
        // Single SaveChanges inserts the showtime and its room together (atomic).
        await _uow.ShowTimeStore.CreateAsync(entity);
        return ToShowTimeDTO(await _uow.ShowTimeStore.GetByIdWithRoomsAsync(entity.Id) ?? entity);
    }

    public async Task<ShowTimeDTO> UpdateAsync(UpdateShowTimeRequest request)
    {
        // Editing an existing showtime doesn't require a future start (an admin may be correcting
        // the record of one that already screened), but the window must still make sense.
        ValidateWindow(request.StartTime, request.EndTime, mustBeFuture: false);

        var entity = await _uow.ShowTimeStore.GetByIdWithRoomsAsync(request.Id);
        if (entity == null)
        {
            throw new KeyNotFoundException($"ShowTime {request.Id} not found.");
        }
        entity.PatchEntity<ShowTime, UpdateShowTimeRequest>(request);
        entity.LastUpdatedTime = DateTime.UtcNow;
        // Availability (movie retired/out-of-window, room/theater inactive) reflects present-day
        // state, not what was true when the showtime ran — so, like ValidateWindow's own
        // mustBeFuture carve-out, it's only enforced when the (possibly just-edited) start time is
        // still in the future. An admin fixing a typo on a historical record isn't blocked by a
        // movie or room that has since gone inactive; moving a past showtime into the future
        // re-arms both checks.
        var enforceAvailability = entity.StartTime > DateTime.Now;
        var movie = await LoadMovieRuntimeAsync(entity.MovieId);
        ValidateAgainstRuntime(entity.StartTime, entity.EndTime, movie);
        if (enforceAvailability)
        {
            ValidateMovieAvailability(entity.StartTime, movie);
        }
        if (request.RoomId != Guid.Empty)
        {
            var roomClass = await LoadRoomClassAsync(request.RoomId);
            if (enforceAvailability)
            {
                ValidateRoomAvailability(roomClass);
            }
            ValidateRoomSupportsFormat(roomClass, entity.ProjectionForm);
            if (await _uow.ShowTimeStore.HasRoomOverlapAsync(request.RoomId, entity.StartTime, entity.EndTime, roomClass.TurnoverBufferMinutes, entity.Id))
            {
                throw new InvalidOperationException(RoomOverlapMessage(roomClass.TurnoverBufferMinutes));
            }
        }
        ApplyRoom(entity, request.RoomId, request.BasePrice);
        // The showtime patch and room change are saved in one transaction on the tracked graph.
        await _uow.SaveChangesAsync();
        return ToShowTimeDTO(await _uow.ShowTimeStore.GetByIdWithRoomsAsync(request.Id) ?? entity);
    }

    public async Task DeleteAsync(Guid id)
    {
        await _uow.ShowTimeStore.DeleteAsync(id);
    }

    /// <summary>
    /// Rejects nonsensical showtime windows. Without this a showtime could be saved ending before
    /// it starts — which also slipped past the room-overlap check, since an inverted window
    /// overlaps nothing — or scheduled into the past where nobody can book it.
    /// </summary>
    private static void ValidateWindow(DateTime startTime, DateTime endTime, bool mustBeFuture)
    {
        if (endTime <= startTime)
        {
            throw new InvalidOperationException("A showtime must end after it starts.");
        }
        if (mustBeFuture && startTime <= DateTime.Now)
        {
            throw new InvalidOperationException("A showtime cannot be scheduled in the past.");
        }
    }

    /// <summary>
    /// Reconciles a showtime's single room assignment. No-ops when nothing changed so that
    /// editing an already-booked showtime (whose room is referenced by invoices) doesn't
    /// attempt a restricted delete.
    /// </summary>
    private static void ApplyRoom(ShowTime entity, Guid roomId, int basePrice)
    {
        if (roomId == Guid.Empty) { return; }

        var current = entity.ShowTimeRooms.FirstOrDefault();
        if (current != null && current.RoomId == roomId && current.BasePrice == basePrice) { return; }

        entity.ShowTimeRooms.Clear();
        entity.ShowTimeRooms.Add(new ShowTimeRoom { ShowTimeId = entity.Id, RoomId = roomId, BasePrice = basePrice });
    }

    /// <summary>Snapshot of the room's commercial class and availability this manager validates
    /// against, loaded once per Create/Update instead of separate tracked Room/Theater fetches.
    /// Public so the store's <c>FindSelectAsync&lt;Class&gt;</c> explicit type argument can be named
    /// from the test project's mock setup.</summary>
    public sealed record RoomClassInfo(string Name, bool SupportsThreeD, int TurnoverBufferMinutes, RoomStatus RoomStatus, string TheaterName, bool TheaterIsActive);

    private async Task<RoomClassInfo> LoadRoomClassAsync(Guid roomId)
    {
        var roomClass = await _uow.RoomStore.FindSelectAsync<RoomClassInfo>(
            r => r.Id == roomId,
            r => new RoomClassInfo(r.RoomType.Name, r.RoomType.SupportsThreeD, r.RoomType.TurnoverBufferMinutes, r.Status, r.Theater.Name, r.Theater.IsActive));
        if (roomClass == null)
        {
            throw new KeyNotFoundException($"Room {roomId} not found.");
        }
        return roomClass;
    }

    /// <summary>Snapshot of the movie properties this manager validates a showtime's window and
    /// availability against. Public for the same reason as <see cref="RoomClassInfo"/> — the test
    /// project's mock setup needs to name the store's <c>FindSelectAsync&lt;Class&gt;</c> explicit
    /// type argument.</summary>
    public sealed record MovieRuntimeInfo(string Title, int Duration, bool IsActive, DateOnly ReleaseDate, DateOnly? EndDate);

    private async Task<MovieRuntimeInfo> LoadMovieRuntimeAsync(Guid movieId)
    {
        var movie = await _uow.MovieStore.FindSelectAsync<MovieRuntimeInfo>(
            m => m.Id == movieId,
            m => new MovieRuntimeInfo(m.Title, m.Duration, m.IsActive, m.ReleaseDate, m.EndDate));
        if (movie == null)
        {
            throw new KeyNotFoundException($"Movie {movieId} not found.");
        }
        return movie;
    }

    /// <summary>
    /// Rejects a showtime that ends before the film itself would be over. A movie with an unset
    /// (zero or negative) duration is a data-quality gap elsewhere, not this check's job to enforce,
    /// so it's skipped rather than blocking every showtime for that movie.
    /// </summary>
    private static void ValidateAgainstRuntime(DateTime startTime, DateTime endTime, MovieRuntimeInfo movie)
    {
        if (movie.Duration <= 0)
        {
            return;
        }

        var earliestEnd = startTime.AddMinutes(movie.Duration);
        if (endTime < earliestEnd)
        {
            throw new InvalidOperationException(
                $"'{movie.Title}' runs {movie.Duration} minutes; the showtime must end no earlier than {earliestEnd:HH:mm}.");
        }
    }

    /// <summary>
    /// Rejects a showtime for a movie that isn't currently available to screen: retired
    /// (<see cref="MovieRuntimeInfo.IsActive"/> false), not yet released, or past the end of its
    /// run. Only the showtime's start DATE is checked — a showtime may start on the release date or
    /// the end date itself (both boundaries inclusive), and the end time is deliberately not gated
    /// here, since <see cref="ValidateAgainstRuntime"/> already lets a final-day screening spill
    /// past midnight. A null <see cref="MovieRuntimeInfo.EndDate"/> means an open-ended run.
    /// </summary>
    private static void ValidateMovieAvailability(DateTime startTime, MovieRuntimeInfo movie)
    {
        if (!movie.IsActive)
        {
            throw new InvalidOperationException($"'{movie.Title}' is no longer showing; reactivate the movie before scheduling it.");
        }

        var showDate = DateOnly.FromDateTime(startTime);
        if (showDate < movie.ReleaseDate)
        {
            throw new InvalidOperationException(
                $"'{movie.Title}' is not released until {movie.ReleaseDate:dd/MM/yyyy}; the showtime cannot start before then.");
        }
        if (movie.EndDate.HasValue && showDate > movie.EndDate.Value)
        {
            throw new InvalidOperationException(
                $"'{movie.Title}' finished its run on {movie.EndDate.Value:dd/MM/yyyy}; the showtime cannot start after then.");
        }
    }

    /// <summary>
    /// Rejects a showtime booked into a room that isn't available: the room itself isn't
    /// <see cref="RoomStatus.Active"/> (covers <see cref="RoomStatus.Maintenance"/>,
    /// <see cref="RoomStatus.Inactive"/>, and defaults to rejecting any future status too), or its
    /// theater is inactive. Checked theater-first, since an inactive theater is the broader cause
    /// and the more useful message when both are true.
    /// </summary>
    private static void ValidateRoomAvailability(RoomClassInfo roomClass)
    {
        if (!roomClass.TheaterIsActive)
        {
            throw new InvalidOperationException($"Theater '{roomClass.TheaterName}' is closed; pick a room in an active theater.");
        }
        if (roomClass.RoomStatus == RoomStatus.Maintenance)
        {
            throw new InvalidOperationException("That room is under maintenance and cannot host a showtime; pick another room.");
        }
        if (roomClass.RoomStatus != RoomStatus.Active)
        {
            throw new InvalidOperationException("That room is not in service; pick another room.");
        }
    }

    /// <summary>
    /// Rejects a 3D screening booked into a room whose class has no 3D projector. The room class and
    /// the screening dimension are independent axes, so nothing else stops the pairing — and it would
    /// surface only at the door, after tickets were sold.
    /// </summary>
    private static void ValidateRoomSupportsFormat(RoomClassInfo roomClass, ProjectionForm form)
    {
        if (form != ProjectionForm.ThreeD)
        {
            return;
        }

        if (!roomClass.SupportsThreeD)
        {
            throw new InvalidOperationException(
                $"Room class '{roomClass.Name}' cannot screen 3D. "
                + "Pick a 3D-capable room or set the showtime to 2D.");
        }
    }

    /// <summary>Distinguishes a true double-booking from a too-tight turnover gap, since the fix an
    /// admin needs differs: pick a different room/time either way, but the second case also names
    /// the class's configured buffer so they know why a seemingly free slot was rejected.</summary>
    private static string RoomOverlapMessage(int bufferMinutes)
        => bufferMinutes > 0
            ? $"This room needs {bufferMinutes} minutes of turnover time between screenings; that slot is too close to an existing showtime."
            : "This room already has a showtime overlapping that time window.";

    private static ShowTimeDTO ToShowTimeDTO(ShowTime s)
    {
        var dto = s.ToDTO<ShowTime, ShowTimeDTO>();
        var sr = s.ShowTimeRooms?.FirstOrDefault();
        if (sr != null)
        {
            dto.RoomId = sr.RoomId;
            dto.RoomName = sr.Room?.Name;
            dto.RoomTypeName = sr.Room?.RoomType?.Name;
            dto.BasePrice = sr.BasePrice;
            dto.TurnoverBufferMinutes = sr.Room?.RoomType?.TurnoverBufferMinutes ?? 0;
        }
        return dto;
    }
}
