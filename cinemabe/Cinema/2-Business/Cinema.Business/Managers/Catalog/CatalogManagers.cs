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
        return e.Code.Contains(kw, StringComparison.OrdinalIgnoreCase)
            || (e.Description ?? string.Empty).Contains(kw, StringComparison.OrdinalIgnoreCase);
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
        await GenerateSeatsAsync(entity.Id, entity.TotalRows, entity.TotalColumns);
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
            await GenerateSeatsAsync(entity.Id, request.TotalRows, request.TotalColumns);
        }
        return entity.ToDTO<Room, RoomDTO>();
    }

    /// <summary>Creates a seat for every cell of the room's row × column grid.</summary>
    private async Task GenerateSeatsAsync(Guid roomId, int rows, int columns)
    {
        if (rows <= 0 || columns <= 0)
            return;

        var seatTypeId = (await Uow.SeatTypeStore.GetAllAsync()).FirstOrDefault()?.Id ?? Guid.Empty;
        if (seatTypeId == Guid.Empty)
            return; // No seat types defined yet — nothing valid to attach seats to.

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
        var entity = request.ToNewEntity<CreateShowTimeRequest, ShowTime>();
        if (request.RoomId != Guid.Empty)
        {
            if (await Uow.ShowTimeStore.HasRoomOverlapAsync(request.RoomId, entity.StartTime, entity.EndTime, null))
                throw new InvalidOperationException("This room already has a showtime overlapping that time window.");
            entity.ShowTimeRooms.Add(new ShowTimeRoom { RoomId = request.RoomId, BasePrice = request.BasePrice });
        }
        // Single SaveChanges inserts the showtime and its room together (atomic).
        await Uow.ShowTimeStore.CreateAsync(entity);
        return ToShowTimeDTO(await Uow.ShowTimeStore.GetByIdWithRoomsAsync(entity.Id) ?? entity);
    }

    public override async Task<ShowTimeDTO> UpdateAsync(UpdateShowTimeRequest request)
    {
        var entity = await Uow.ShowTimeStore.GetByIdWithRoomsAsync(request.Id)
                     ?? throw new KeyNotFoundException($"ShowTime {request.Id} not found.");
        entity.PatchEntity<ShowTime, UpdateShowTimeRequest>(request);
        entity.LastUpdatedTime = DateTime.UtcNow;
        if (request.RoomId != Guid.Empty &&
            await Uow.ShowTimeStore.HasRoomOverlapAsync(request.RoomId, entity.StartTime, entity.EndTime, entity.Id))
            throw new InvalidOperationException("This room already has a showtime overlapping that time window.");
        ApplyRoom(entity, request.RoomId, request.BasePrice);
        // The showtime patch and room change are saved in one transaction on the tracked graph.
        await Uow.SaveChangesAsync();
        return ToShowTimeDTO(await Uow.ShowTimeStore.GetByIdWithRoomsAsync(request.Id) ?? entity);
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
