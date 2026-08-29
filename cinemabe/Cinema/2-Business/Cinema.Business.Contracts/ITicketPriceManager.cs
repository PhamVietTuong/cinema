using Cinema.Business.DTO.Catalog;

namespace Cinema.Business.Contracts;

public interface ITicketPriceManager : ICatalogManager<TicketPriceDTO, CreateTicketPriceRequest, UpdateTicketPriceRequest> { }
