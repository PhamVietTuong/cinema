using Cinema.Business.DTO.Catalog;

namespace Cinema.Business.Contracts;

public interface ISeatTypeManager : ICatalogManager<SeatTypeDTO, CreateSeatTypeRequest, UpdateSeatTypeRequest> { }
