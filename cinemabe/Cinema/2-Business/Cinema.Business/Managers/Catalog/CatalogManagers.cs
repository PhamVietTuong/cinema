using Cinema.Business.Contracts;
using Cinema.Business.DTO.Catalog;
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
}
