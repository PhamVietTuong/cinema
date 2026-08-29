using Cinema.Business.Contracts;
using Cinema.Business.DTO.Catalog;
using Cinema.Business.DTO.Requests;
using Cinema.Business.Extensions;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;

namespace Cinema.Business.Managers;

// Concrete catalog managers. Each binds the generic CatalogManager to its entity,
// points at the backing store, and defines the keyword search field(s).

public class AgeRestrictionManager(IApplicationUnitOfWork uow)
    : CatalogManager<AgeRestriction, AgeRestrictionDTO, CreateAgeRestrictionRequest, UpdateAgeRestrictionRequest>(uow), IAgeRestrictionManager
{
    protected override IGenericStore<AgeRestriction> Store => Uow.AgeRestrictionStore;

    protected override bool Match(AgeRestriction e, string kw)
    {
        return e.Code.Contains(kw, StringComparison.OrdinalIgnoreCase)
            || e.Description.Contains(kw, StringComparison.OrdinalIgnoreCase);
    }
}

public class DiscountTypeManager(IApplicationUnitOfWork uow)
    : CatalogManager<DiscountType, DiscountTypeDTO, CreateDiscountTypeRequest, UpdateDiscountTypeRequest>(uow), IDiscountTypeManager
{
    protected override IGenericStore<DiscountType> Store => Uow.DiscountTypeStore;

    protected override bool Match(DiscountType e, string kw)
    {
        return e.Name.Contains(kw, StringComparison.OrdinalIgnoreCase);
    }
}

public class MovieTypeManager(IApplicationUnitOfWork uow)
    : CatalogManager<MovieType, MovieTypeDTO, CreateMovieTypeRequest, UpdateMovieTypeRequest>(uow), IMovieTypeManager
{
    protected override IGenericStore<MovieType> Store => Uow.MovieTypeStore;

    protected override bool Match(MovieType e, string kw)
    {
        return e.Name.Contains(kw, StringComparison.OrdinalIgnoreCase);
    }
}

public class SeatTypeManager(IApplicationUnitOfWork uow)
    : CatalogManager<SeatType, SeatTypeDTO, CreateSeatTypeRequest, UpdateSeatTypeRequest>(uow), ISeatTypeManager
{
    protected override IGenericStore<SeatType> Store => Uow.SeatTypeStore;

    protected override bool Match(SeatType e, string kw)
    {
        return e.Name.Contains(kw, StringComparison.OrdinalIgnoreCase);
    }
}

public class UserTypeManager(IApplicationUnitOfWork uow)
    : CatalogManager<UserType, UserTypeDTO, CreateUserTypeRequest, UpdateUserTypeRequest>(uow), IUserTypeManager
{
    protected override IGenericStore<UserType> Store => Uow.UserTypeStore;

    protected override bool Match(UserType e, string kw)
    {
        return e.Name.Contains(kw, StringComparison.OrdinalIgnoreCase);
    }
}

public class MemberShipManager(IApplicationUnitOfWork uow)
    : CatalogManager<MemberShip, MemberShipDTO, CreateMemberShipRequest, UpdateMemberShipRequest>(uow), IMemberShipManager
{
    protected override IGenericStore<MemberShip> Store => Uow.MemberShipStore;

    protected override bool Match(MemberShip e, string kw)
    {
        return e.Name.Contains(kw, StringComparison.OrdinalIgnoreCase);
    }
}

public class HolidayManager(IApplicationUnitOfWork uow)
    : CatalogManager<Holiday, HolidayDTO, CreateHolidayRequest, UpdateHolidayRequest>(uow), IHolidayManager
{
    protected override IGenericStore<Holiday> Store => Uow.HolidayStore;

    protected override bool Match(Holiday e, string kw)
    {
        return e.Name.Contains(kw, StringComparison.OrdinalIgnoreCase);
    }
}

public class NewsManager(IApplicationUnitOfWork uow)
    : CatalogManager<News, NewsDTO, CreateNewsRequest, UpdateNewsRequest>(uow), INewsManager
{
    protected override IGenericStore<News> Store => Uow.NewsStore;

    protected override bool Match(News e, string kw)
    {
        return e.Title.Contains(kw, StringComparison.OrdinalIgnoreCase);
    }
}

public class DiscountManager(IApplicationUnitOfWork uow)
    : CatalogManager<Discount, DiscountDTO, CreateDiscountRequest, UpdateDiscountRequest>(uow), IDiscountManager
{
    protected override IGenericStore<Discount> Store => Uow.DiscountStore;

    protected override bool Match(Discount e, string kw)
    {
        return (e.Code ?? string.Empty).Contains(kw, StringComparison.OrdinalIgnoreCase)
            || (e.Description ?? string.Empty).Contains(kw, StringComparison.OrdinalIgnoreCase);
    }

    // Overridden so the promotion's theater scope (join rows) is loaded and projected.
    public override async Task<DefaultSearchResults<DiscountDTO>> GetAsync(PagingSearchDTO search)
    {
        search ??= new PagingSearchDTO();
        var all = await Uow.DiscountStore.GetAllWithScopeAsync();

        var keyword = search.Filters.GetString("keyword");
        if (!string.IsNullOrWhiteSpace(keyword))
            all = all.Where(e => Match(e, keyword)).ToList();

        var page = search.PageIndex > 0 ? search.PageIndex : 1;
        var pageSize = search.PageSize > 0 ? search.PageSize : 20;
        var total = all.Count;
        var paged = all.Skip((page - 1) * pageSize).Take(pageSize).Select(ToDiscountDTO).ToList();

        return new DefaultSearchResults<DiscountDTO>
        {
            Results = paged, TotalCount = total, CountPerPage = pageSize, Page = page
        };
    }

    public override async Task<DiscountDTO> GetByIdAsync(Guid id)
    {
        var entity = await Uow.DiscountStore.GetByIdWithScopeAsync(id)
                     ?? throw new KeyNotFoundException($"Discount {id} not found.");
        return ToDiscountDTO(entity);
    }

    public override async Task<DiscountDTO> CreateAsync(CreateDiscountRequest request)
    {
        var entity = request.ToNewEntity<CreateDiscountRequest, Discount>();
        entity.Code = Normalize(request.Code);
        entity.StartTimeOfDay = ParseTime(request.StartTimeOfDay);
        entity.EndTimeOfDay = ParseTime(request.EndTimeOfDay);
        ApplyTheaterScope(entity, request.ApplyToAllTheaters, request.TheaterIds);
        await Uow.DiscountStore.CreateAsync(entity);
        return ToDiscountDTO(entity);
    }

    public override async Task<DiscountDTO> UpdateAsync(UpdateDiscountRequest request)
    {
        var entity = await Uow.DiscountStore.GetByIdWithScopeAsync(request.Id)
                     ?? throw new KeyNotFoundException($"Discount {request.Id} not found.");

        entity.PatchEntity<Discount, UpdateDiscountRequest>(request);
        // Explicitly assign nullable/renamed fields the reflection patch skips when they're null.
        entity.Code = Normalize(request.Code);
        entity.Description = request.Description;
        entity.MaxDiscountAmount = request.MaxDiscountAmount;
        entity.MaxUsage = request.MaxUsage;
        entity.MovieId = request.MovieId;
        entity.DaysOfWeekMask = request.DaysOfWeekMask;
        entity.StartTimeOfDay = ParseTime(request.StartTimeOfDay);
        entity.EndTimeOfDay = ParseTime(request.EndTimeOfDay);
        entity.LastUpdatedTime = DateTime.UtcNow;
        ApplyTheaterScope(entity, request.ApplyToAllTheaters, request.TheaterIds);

        // entity is tracked (loaded with scope) — one SaveChanges persists the patch and join add/removes.
        await Uow.SaveChangesAsync();
        return ToDiscountDTO(entity);
    }

    /// <summary>Reconciles the promotion's theater join rows to the requested set.</summary>
    private static void ApplyTheaterScope(Discount entity, bool applyToAll, List<Guid>? theaterIds)
    {
        entity.ApplyToAllTheaters = applyToAll;
        var wanted = applyToAll || theaterIds == null
            ? new HashSet<Guid>()
            : theaterIds.Where(id => id != Guid.Empty).ToHashSet();

        foreach (var link in entity.DiscountTheaters.Where(t => !wanted.Contains(t.TheaterId)).ToList())
            entity.DiscountTheaters.Remove(link);

        var existing = entity.DiscountTheaters.Select(t => t.TheaterId).ToHashSet();
        foreach (var id in wanted.Where(id => !existing.Contains(id)))
            entity.DiscountTheaters.Add(new DiscountTheater { TheaterId = id });
    }

    private static string? Normalize(string? code)
        => string.IsNullOrWhiteSpace(code) ? null : code.Trim();

    private static TimeOnly? ParseTime(string? value)
        => TimeOnly.TryParse(value, out var t) ? t : null;

    private static DiscountDTO ToDiscountDTO(Discount d)
    {
        var dto = d.ToDTO<Discount, DiscountDTO>();
        dto.TheaterIds = d.DiscountTheaters?.Select(t => t.TheaterId).ToList() ?? new();
        dto.StartTimeOfDay = d.StartTimeOfDay?.ToString("HH:mm");
        dto.EndTimeOfDay = d.EndTimeOfDay?.ToString("HH:mm");
        return dto;
    }
}

public class FoodAndDrinkManager(IApplicationUnitOfWork uow)
    : CatalogManager<FoodAndDrink, FoodAndDrinkDTO, CreateFoodAndDrinkRequest, UpdateFoodAndDrinkRequest>(uow), IFoodAndDrinkManager
{
    protected override IGenericStore<FoodAndDrink> Store => Uow.FoodAndDrinkStore;

    protected override bool Match(FoodAndDrink e, string kw)
    {
        return e.Name.Contains(kw, StringComparison.OrdinalIgnoreCase);
    }
}

public class RoomTypeManager(IApplicationUnitOfWork uow)
    : CatalogManager<RoomType, RoomTypeDTO, CreateRoomTypeRequest, UpdateRoomTypeRequest>(uow), IRoomTypeManager
{
    protected override IGenericStore<RoomType> Store => Uow.RoomTypeStore;

    protected override bool Match(RoomType e, string kw)
    {
        return e.Name.Contains(kw, StringComparison.OrdinalIgnoreCase);
    }
}

public class TimeSlotManager(IApplicationUnitOfWork uow)
    : CatalogManager<TimeSlot, TimeSlotDTO, CreateTimeSlotRequest, UpdateTimeSlotRequest>(uow), ITimeSlotManager
{
    protected override IGenericStore<TimeSlot> Store => Uow.TimeSlotStore;

    protected override bool Match(TimeSlot e, string kw)
    {
        return e.Name.Contains(kw, StringComparison.OrdinalIgnoreCase);
    }
}

public class TicketPriceManager(IApplicationUnitOfWork uow)
    : CatalogManager<TicketPrice, TicketPriceDTO, CreateTicketPriceRequest, UpdateTicketPriceRequest>(uow), ITicketPriceManager
{
    protected override IGenericStore<TicketPrice> Store => Uow.TicketPriceStore;

    // No free-text field to search; keyword search is a no-op (per-column filters still apply).
    protected override bool Match(TicketPrice e, string kw) => true;
}

public class RoomManager(IApplicationUnitOfWork uow)
    : CatalogManager<Room, RoomDTO, CreateRoomRequest, UpdateRoomRequest>(uow), IRoomManager
{
    protected override IGenericStore<Room> Store => Uow.RoomStore;

    protected override bool Match(Room e, string kw)
    {
        return e.Name.Contains(kw, StringComparison.OrdinalIgnoreCase);
    }

    public override async Task<RoomDTO> CreateAsync(CreateRoomRequest request)
    {
        var entity = request.ToNewEntity<CreateRoomRequest, Room>();
        await Uow.RoomStore.CreateAsync(entity);
        await GenerateSeatsAsync(entity.Id, entity.TheaterId, entity.TotalRows, entity.TotalColumns);
        return entity.ToDTO<Room, RoomDTO>();
    }

    public override async Task<RoomDTO> UpdateAsync(UpdateRoomRequest request)
    {
        var entity = await Uow.RoomStore.GetByIdAsync(request.Id)
                     ?? throw new KeyNotFoundException($"Room {request.Id} not found.");
        var dimensionsChanged = entity.TotalRows != request.TotalRows || entity.TotalColumns != request.TotalColumns;
        entity.PatchEntity<Room, UpdateRoomRequest>(request);
        await Uow.RoomStore.UpdateAsync(entity);

        // Rebuild the seat map only when the grid size actually changes.
        if (dimensionsChanged)
        {
            var existing = await Uow.SeatStore.FindAsync(s => s.RoomId == entity.Id);
            foreach (var seat in existing)
                await Uow.SeatStore.DeleteAsync(seat.Id);
            await GenerateSeatsAsync(entity.Id, entity.TheaterId, request.TotalRows, request.TotalColumns);
        }
        return entity.ToDTO<Room, RoomDTO>();
    }

    /// <summary>Creates a seat for every cell of the room's row × column grid.</summary>
    private async Task GenerateSeatsAsync(Guid roomId, Guid theaterId, int rows, int columns)
    {
        if (rows <= 0 || columns <= 0)
            return;

        var seatTypeId = (await Uow.SeatTypeStore.FindAsync(st => st.TheaterId == theaterId)).FirstOrDefault()?.Id ?? Guid.Empty;
        if (seatTypeId == Guid.Empty)
            return; // No seat types for this theater yet — nothing valid to attach seats to.

        var seats = new List<Seat>();
        for (var r = 0; r < rows; r++)
        {
            var rowName = ToRowName(r);
            for (var c = 1; c <= columns; c++)
            {
                seats.Add(new Seat
                {
                    RoomId     = roomId,
                    RowName    = rowName,
                    ColIndex   = c,
                    SeatTypeId = seatTypeId,
                    IsActive   = true,
                });
            }
        }
        await Uow.SeatStore.CreateRangeAsync(seats);
    }

    /// <summary>0 → "A", 25 → "Z", 26 → "AA", …</summary>
    private static string ToRowName(int index)
    {
        var name = string.Empty;
        index++;
        while (index > 0)
        {
            index--;
            name = (char)('A' + index % 26) + name;
            index /= 26;
        }
        return name;
    }

    public async Task<List<RoomSeatDTO>> GetSeatMapAsync(Guid roomId)
    {
        var seats = await Uow.SeatStore.FindAsync(s => s.RoomId == roomId);
        var types = (await Uow.SeatTypeStore.GetAllAsync()).ToDictionary(t => t.Id);
        return seats
            .OrderBy(s => s.RowName).ThenBy(s => s.ColIndex)
            .Select(s =>
            {
                types.TryGetValue(s.SeatTypeId, out var t);
                return new RoomSeatDTO
                {
                    Id            = s.Id,
                    RowName       = s.RowName,
                    ColIndex      = s.ColIndex,
                    SeatTypeId    = s.SeatTypeId,
                    SeatTypeName  = t?.Name ?? string.Empty,
                    SeatTypeColor = t?.Color ?? "#808080",
                    PriceMultiplier = t?.PriceMultiplier ?? 1,
                    SeatGroupId   = s.SeatGroupId,
                    IsActive      = s.IsActive,
                };
            })
            .ToList();
    }

    public async Task SaveSeatMapAsync(SaveSeatMapRequest request)
    {
        // FindAsync returns tracked entities on the shared context, so mutating them
        // and saving once persists every assignment in a single round-trip.
        var seats = (await Uow.SeatStore.FindAsync(s => s.RoomId == request.RoomId))
            .ToDictionary(s => s.Id);
        foreach (var item in request.Seats)
        {
            if (!seats.TryGetValue(item.SeatId, out var seat)) continue;
            seat.SeatTypeId  = item.SeatTypeId;
            seat.SeatGroupId = item.SeatGroupId;
            seat.IsActive    = item.IsActive;
        }
        await Uow.SaveChangesAsync();
    }

    public async Task<List<RoomSeatDTO>> ResizeSeatGridAsync(ResizeSeatGridRequest request)
    {
        var room = await Uow.RoomStore.GetByIdAsync(request.RoomId)
                   ?? throw new KeyNotFoundException($"Room {request.RoomId} not found.");

        var rows = Math.Max(0, request.TotalRows);
        var columns = Math.Max(0, request.TotalColumns);

        // Cells the grid should contain after the resize.
        var desired = new HashSet<(string Row, int Col)>();
        for (var r = 0; r < rows; r++)
        {
            var rowName = ToRowName(r);
            for (var c = 1; c <= columns; c++)
                desired.Add((rowName, c));
        }

        var existing = (await Uow.SeatStore.FindAsync(s => s.RoomId == room.Id)).ToList();

        // Drop seats that fall outside the new grid (trimmed rows/columns).
        foreach (var seat in existing.Where(s => !desired.Contains((s.RowName, s.ColIndex))))
            await Uow.SeatStore.DeleteAsync(seat.Id);

        // Create seats for the appended cells; seats that stay keep their type + grouping.
        var present = existing.Select(s => (s.RowName, s.ColIndex)).ToHashSet();
        var seatTypeId = (await Uow.SeatTypeStore.FindAsync(st => st.TheaterId == room.TheaterId)).FirstOrDefault()?.Id ?? Guid.Empty;
        if (seatTypeId != Guid.Empty)
        {
            var toAdd = desired
                .Where(cell => !present.Contains(cell))
                .Select(cell => new Seat
                {
                    RoomId     = room.Id,
                    RowName    = cell.Row,
                    ColIndex   = cell.Col,
                    SeatTypeId = seatTypeId,
                    IsActive   = true,
                })
                .ToList();
            if (toAdd.Count > 0)
                await Uow.SeatStore.CreateRangeAsync(toAdd);
        }

        // Keep the room's stored dimensions in step with its seat grid.
        room.TotalRows = rows;
        room.TotalColumns = columns;
        await Uow.RoomStore.UpdateAsync(room);

        return await GetSeatMapAsync(room.Id);
    }
}

public class ShowTimeManager(IApplicationUnitOfWork uow)
    : CatalogManager<ShowTime, ShowTimeDTO, CreateShowTimeRequest, UpdateShowTimeRequest>(uow), IShowTimeManager
{
    protected override IGenericStore<ShowTime> Store => Uow.ShowTimeStore;

    public override async Task<DefaultSearchResults<ShowTimeDTO>> GetAsync(PagingSearchDTO search)
    {
        search ??= new PagingSearchDTO();
        var page = search.PageIndex > 0 ? search.PageIndex : 1;
        var pageSize = search.PageSize > 0 ? search.PageSize : 20;

        var (items, total) = await Uow.ShowTimeStore.SearchAsync(
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

    public override async Task<ShowTimeDTO> GetByIdAsync(Guid id)
    {
        var entity = await Uow.ShowTimeStore.GetByIdWithRoomsAsync(id)
                     ?? throw new KeyNotFoundException($"ShowTime {id} not found.");
        return ToShowTimeDTO(entity);
    }

    public override async Task<ShowTimeDTO> CreateAsync(CreateShowTimeRequest request)
    {
        ValidateWindow(request.StartTime, request.EndTime, mustBeFuture: true);

        var entity = request.ToNewEntity<CreateShowTimeRequest, ShowTime>();
        if (request.RoomId != Guid.Empty)
        {
            if (await Uow.ShowTimeStore.HasRoomOverlapAsync(request.RoomId, entity.StartTime, entity.EndTime, null))
            {
                throw new InvalidOperationException("This room already has a showtime overlapping that time window.");
            }
            entity.ShowTimeRooms.Add(new ShowTimeRoom { RoomId = request.RoomId, BasePrice = request.BasePrice });
        }
        // Single SaveChanges inserts the showtime and its room together (atomic).
        await Uow.ShowTimeStore.CreateAsync(entity);
        return ToShowTimeDTO(await Uow.ShowTimeStore.GetByIdWithRoomsAsync(entity.Id) ?? entity);
    }

    public override async Task<ShowTimeDTO> UpdateAsync(UpdateShowTimeRequest request)
    {
        // Editing an existing showtime doesn't require a future start (an admin may be correcting
        // the record of one that already screened), but the window must still make sense.
        ValidateWindow(request.StartTime, request.EndTime, mustBeFuture: false);

        var entity = await Uow.ShowTimeStore.GetByIdWithRoomsAsync(request.Id)
                     ?? throw new KeyNotFoundException($"ShowTime {request.Id} not found.");
        entity.PatchEntity<ShowTime, UpdateShowTimeRequest>(request);
        entity.LastUpdatedTime = DateTime.UtcNow;
        if (request.RoomId != Guid.Empty &&
            await Uow.ShowTimeStore.HasRoomOverlapAsync(request.RoomId, entity.StartTime, entity.EndTime, entity.Id))
        {
            throw new InvalidOperationException("This room already has a showtime overlapping that time window.");
        }
        ApplyRoom(entity, request.RoomId, request.BasePrice);
        // The showtime patch and room change are saved in one transaction on the tracked graph.
        await Uow.SaveChangesAsync();
        return ToShowTimeDTO(await Uow.ShowTimeStore.GetByIdWithRoomsAsync(request.Id) ?? entity);
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

    private static ShowTimeDTO ToShowTimeDTO(ShowTime s)
    {
        var dto = s.ToDTO<ShowTime, ShowTimeDTO>();
        var sr = s.ShowTimeRooms?.FirstOrDefault();
        if (sr != null)
        {
            dto.RoomId = sr.RoomId;
            dto.RoomName = sr.Room?.Name;
            dto.BasePrice = sr.BasePrice;
        }
        return dto;
    }
}
