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

public class TicketTypeManager(IApplicationUnitOfWork uow)
    : CatalogManager<TicketType, TicketTypeDTO, CreateTicketTypeRequest, UpdateTicketTypeRequest>(uow), ITicketTypeManager
{
    protected override IGenericStore<TicketType> Store => Uow.TicketTypeStore;

    protected override bool Match(TicketType e, string kw)
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
