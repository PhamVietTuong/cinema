using Cinema.Business.DTO.Catalog;

namespace Cinema.Business.Contracts;

public interface IHolidayManager : ICatalogManager<HolidayDTO, CreateHolidayRequest, UpdateHolidayRequest> { }
