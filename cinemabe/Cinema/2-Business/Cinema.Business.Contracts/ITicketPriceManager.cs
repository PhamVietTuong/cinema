using Cinema.Business.DTO.Catalog;
using Cinema.Business.DTO.Requests;
using Cinema.Data.Entities;

namespace Cinema.Business.Contracts;

public interface ITicketPriceManager
{
    Task<DefaultSearchResults<TicketPriceDTO>> GetAsync(PagingSearchDTO search);
    Task<bool>                                 ExistsAsync(Guid id);
    Task<TicketPriceDTO>                       GetByIdAsync(Guid id);
    Task<TicketPriceDTO>                       CreateAsync(CreateTicketPriceRequest request);
    Task<TicketPriceDTO>                       UpdateAsync(UpdateTicketPriceRequest request);
    Task                                       DeleteAsync(Guid id);
}
