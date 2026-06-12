using Cinema.Business.DTO.Catalog;
using Cinema.Business.DTO.Requests;
using Cinema.Data.Entities;

namespace Cinema.Business.Contracts;

/// <summary>CRUD for the Movie ↔ MovieType join table (composite key, no surrogate Id).</summary>
public interface IMovieTypeDetailManager
{
    Task<DefaultSearchResults<MovieTypeDetailDTO>> GetAsync(PagingSearchDTO search);
    Task<MovieTypeDetailDTO> CreateAsync(CreateMovieTypeDetailRequest request);
    Task DeleteAsync(Guid movieId, Guid movieTypeId);
}

/// <summary>CRUD for the SeatType ↔ TicketType price matrix (composite key).</summary>
public interface ISeatTypeTicketTypeManager
{
    Task<DefaultSearchResults<SeatTypeTicketTypeDTO>> GetAsync(PagingSearchDTO search);
    Task<SeatTypeTicketTypeDTO> CreateAsync(CreateSeatTypeTicketTypeRequest request);
    Task<SeatTypeTicketTypeDTO> UpdateAsync(UpdateSeatTypeTicketTypeRequest request);
    Task DeleteAsync(Guid seatTypeId, Guid ticketTypeId);
}

/// <summary>Admin view over invoices: list, change status, delete. Invoices are created by booking.</summary>
public interface IInvoiceAdminManager
{
    Task<DefaultSearchResults<InvoiceAdminDTO>> GetAsync(PagingSearchDTO search);
    Task<InvoiceAdminDTO> UpdateStatusAsync(UpdateInvoiceStatusRequest request);
    Task DeleteAsync(Guid id);
}
