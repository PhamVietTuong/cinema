using Cinema.Business.DTO.Catalog;
using Cinema.Business.DTO.Requests;
using Cinema.Data.Entities;

namespace Cinema.Business.Contracts;

public interface ISeatTypeManager
{
    Task<DefaultSearchResults<SeatTypeDTO>> GetAsync(PagingSearchDTO search);
    Task<bool>                              ExistsAsync(Guid id);
    Task<SeatTypeDTO>                       GetByIdAsync(Guid id);
    Task<SeatTypeDTO>                       CreateAsync(CreateSeatTypeRequest request);
    Task<SeatTypeDTO>                       UpdateAsync(UpdateSeatTypeRequest request);
    Task                                    DeleteAsync(Guid id);
}
