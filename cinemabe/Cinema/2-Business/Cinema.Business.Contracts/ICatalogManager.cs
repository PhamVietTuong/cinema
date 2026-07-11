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
public interface IUserTypeManager       : ICatalogManager<UserTypeDTO,       CreateUserTypeRequest,       UpdateUserTypeRequest> { }
public interface IMemberShipManager     : ICatalogManager<MemberShipDTO,     CreateMemberShipRequest,     UpdateMemberShipRequest> { }
public interface IHolidayManager        : ICatalogManager<HolidayDTO,        CreateHolidayRequest,        UpdateHolidayRequest> { }
public interface INewsManager           : ICatalogManager<NewsDTO,           CreateNewsRequest,           UpdateNewsRequest> { }
public interface IDiscountManager       : ICatalogManager<DiscountDTO,       CreateDiscountRequest,       UpdateDiscountRequest> { }
public interface IFoodAndDrinkManager   : ICatalogManager<FoodAndDrinkDTO,   CreateFoodAndDrinkRequest,   UpdateFoodAndDrinkRequest> { }
public interface IRoomManager           : ICatalogManager<RoomDTO,           CreateRoomRequest,           UpdateRoomRequest>
{
    /// <summary>Returns every seat of a room with its current type + grouping, for the seat-map editor.</summary>
    Task<List<RoomSeatDTO>> GetSeatMapAsync(Guid roomId);
    /// <summary>Persists seat-type and double-seat grouping changes for a room's existing seats.</summary>
    Task SaveSeatMapAsync(SaveSeatMapRequest request);
    /// <summary>Adds/removes rows or columns of seats, preserving the seats that stay in the grid. Returns the updated seat map.</summary>
    Task<List<RoomSeatDTO>> ResizeSeatGridAsync(ResizeSeatGridRequest request);
}
public interface IShowTimeManager       : ICatalogManager<ShowTimeDTO,       CreateShowTimeRequest,       UpdateShowTimeRequest> { }
public interface IRoomTypeManager       : ICatalogManager<RoomTypeDTO,       CreateRoomTypeRequest,       UpdateRoomTypeRequest> { }
public interface ITimeSlotManager       : ICatalogManager<TimeSlotDTO,       CreateTimeSlotRequest,       UpdateTimeSlotRequest> { }
public interface ITicketPriceManager    : ICatalogManager<TicketPriceDTO,    CreateTicketPriceRequest,    UpdateTicketPriceRequest> { }
