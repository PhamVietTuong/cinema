using Cinema.Business.DTO.Catalog;

namespace Cinema.Business.Contracts;

public interface IRoomTypeManager : ICatalogManager<RoomTypeDTO, CreateRoomTypeRequest, UpdateRoomTypeRequest> { }
