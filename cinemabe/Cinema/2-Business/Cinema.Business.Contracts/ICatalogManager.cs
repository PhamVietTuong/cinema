using Cinema.Business.DTO.Catalog;
using Cinema.Business.DTO.Requests;
using Cinema.Data.Entities;

namespace Cinema.Business.Contracts;

/// <summary>Standard CRUD surface for a simple lookup ("catalog") entity.</summary>
public interface ICatalogManager<TDto, TCreate, TUpdate>
{
    Task<DefaultSearchResults<TDto>> GetAsync(PagingSearchDTO search);
    Task<TDto>                       GetByIdAsync(Guid id);
    Task<TDto>                       CreateAsync(TCreate request);
    Task<TDto>                       UpdateAsync(TUpdate request);
    Task                             DeleteAsync(Guid id);
}

// ── Per-entity marker interfaces (DI keys + readable controller injection) ──────
public interface IAgeRestrictionManager : ICatalogManager<AgeRestrictionDTO, CreateAgeRestrictionRequest, UpdateAgeRestrictionRequest> { }
public interface IDiscountTypeManager   : ICatalogManager<DiscountTypeDTO,   CreateDiscountTypeRequest,   UpdateDiscountTypeRequest> { }
public interface IMovieTypeManager      : ICatalogManager<MovieTypeDTO,      CreateMovieTypeRequest,      UpdateMovieTypeRequest> { }
public interface ISeatTypeManager       : ICatalogManager<SeatTypeDTO,       CreateSeatTypeRequest,       UpdateSeatTypeRequest> { }
public interface ITicketTypeManager     : ICatalogManager<TicketTypeDTO,     CreateTicketTypeRequest,     UpdateTicketTypeRequest> { }
public interface IUserTypeManager       : ICatalogManager<UserTypeDTO,       CreateUserTypeRequest,       UpdateUserTypeRequest> { }
public interface IMemberShipManager     : ICatalogManager<MemberShipDTO,     CreateMemberShipRequest,     UpdateMemberShipRequest> { }
public interface IHolidayManager        : ICatalogManager<HolidayDTO,        CreateHolidayRequest,        UpdateHolidayRequest> { }
public interface INewsManager           : ICatalogManager<NewsDTO,           CreateNewsRequest,           UpdateNewsRequest> { }
public interface IDiscountManager       : ICatalogManager<DiscountDTO,       CreateDiscountRequest,       UpdateDiscountRequest> { }
public interface IFoodAndDrinkManager   : ICatalogManager<FoodAndDrinkDTO,   CreateFoodAndDrinkRequest,   UpdateFoodAndDrinkRequest> { }
public interface IRoomManager           : ICatalogManager<RoomDTO,           CreateRoomRequest,           UpdateRoomRequest> { }
public interface IShowTimeManager       : ICatalogManager<ShowTimeDTO,       CreateShowTimeRequest,       UpdateShowTimeRequest> { }
