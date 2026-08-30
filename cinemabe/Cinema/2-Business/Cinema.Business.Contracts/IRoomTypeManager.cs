using Cinema.Business.DTO.Catalog;
using Cinema.Business.DTO.Requests;
using Cinema.Data.Entities;

namespace Cinema.Business.Contracts;

public interface IRoomTypeManager
{
    Task<DefaultSearchResults<RoomTypeDTO>> GetAsync(PagingSearchDTO search);
    Task<bool>                              ExistsAsync(Guid id);
    Task<RoomTypeDTO>                       GetByIdAsync(Guid id);
    Task<RoomTypeDTO>                       CreateAsync(CreateRoomTypeRequest request);
    Task<RoomTypeDTO>                       UpdateAsync(UpdateRoomTypeRequest request);
    Task                                    DeleteAsync(Guid id);
}
