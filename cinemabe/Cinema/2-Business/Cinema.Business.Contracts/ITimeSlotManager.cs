using Cinema.Business.DTO.Catalog;

namespace Cinema.Business.Contracts;

public interface ITimeSlotManager : ICatalogManager<TimeSlotDTO, CreateTimeSlotRequest, UpdateTimeSlotRequest> { }
