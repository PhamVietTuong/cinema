using Cinema.Business.DTO.Catalog;
using Cinema.Business.DTO.Requests;
using Cinema.Data.Entities;

namespace Cinema.Business.Contracts;

public interface ITimeSlotManager
{
    Task<DefaultSearchResults<TimeSlotDTO>> GetAsync(PagingSearchDTO search);
    Task<bool>                              ExistsAsync(Guid id);
    Task<TimeSlotDTO>                       GetByIdAsync(Guid id);
    Task<TimeSlotDTO>                       CreateAsync(CreateTimeSlotRequest request);
    Task<TimeSlotDTO>                       UpdateAsync(UpdateTimeSlotRequest request);
    Task                                    DeleteAsync(Guid id);
}
