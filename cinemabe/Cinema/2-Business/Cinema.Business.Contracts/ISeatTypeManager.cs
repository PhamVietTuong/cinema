using Cinema.Business.DTO.Catalog;
using Cinema.Business.DTO.Requests;
using Cinema.Data.Entities;

namespace Cinema.Business.Contracts;

public interface ISeatTypeManager
{
    Task<DefaultSearchResults<SeatTypeDTO>> GetAsync(PagingSearchDTO search);
    Task<bool>                              ExistsAsync(Guid id);
    Task<SeatTypeDTO>                       GetByIdAsync(Guid id);
    Task<SeatTypeDTO>                       UpdateAsync(UpdateSeatTypeRequest request);

    /// <summary>Idempotently creates the theater's fixed Standard + Double rows if they don't already
    /// exist. Called when a theater is created — there is no other way to create a SeatType row.</summary>
    Task EnsureDefaultsAsync(Guid theaterId);
}
