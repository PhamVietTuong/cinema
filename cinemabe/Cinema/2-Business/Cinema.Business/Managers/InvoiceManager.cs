using Cinema.Business.Contracts;
using Cinema.Business.DTO;
using Cinema.Business.DTO.Invoices;
using Cinema.Business.DTO.Requests;
using Cinema.Business.Extensions;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Cinema.Data.Enums;

namespace Cinema.Business.Managers;

public class InvoiceManager : IInvoiceManager
{
    private readonly IApplicationUnitOfWork _uow;

    public InvoiceManager(IApplicationUnitOfWork uow) => _uow = uow;

    public async Task<DefaultSearchResults<InvoiceDTO>> GetMyInvoicesAsync(Guid userId, PagingSearchDTO search)
    {
        var page     = search.PageIndex > 0 ? search.PageIndex : 1;
        var pageSize = search.PageSize  > 0 ? search.PageSize  : 10;

        var (items, total) = await _uow.InvoiceStore.GetByUserAsync(userId, page, pageSize);
        return new DefaultSearchResults<InvoiceDTO>
        {
            Results      = items.Select(ToInvoiceDTO),
            TotalCount   = total,
            CountPerPage = pageSize,
            Page         = page
        };
    }

    public async Task<DefaultSearchResults<InvoiceDTO>> GetInvoicesAsync(PagingSearchDTO search)
    {
        var status   = search.Filters.GetEnum<InvoiceStatus>("status");
        var from     = search.Filters.GetDateTime("from");
        var to       = search.Filters.GetDateTime("to");
        var page     = search.PageIndex > 0 ? search.PageIndex : 1;
        var pageSize = search.PageSize  > 0 ? search.PageSize  : 20;

        var (items, total) = await _uow.InvoiceStore.GetPagedAsync(status, from, to, page, pageSize);
        return new DefaultSearchResults<InvoiceDTO>
        {
            Results      = items.Select(ToInvoiceDTO),
            TotalCount   = total,
            CountPerPage = pageSize,
            Page         = page
        };
    }

    public async Task<InvoiceDTO> GetByIdAsync(Guid id, Guid requestingUserId, bool isAdmin)
    {
        var invoice = await _uow.InvoiceStore.GetWithDetailsAsync(id)
                      ?? throw new KeyNotFoundException($"Invoice {id} not found.");
        // Object-level authorization: only the owner (or an admin) may read an invoice.
        if (!isAdmin && invoice.UserId != requestingUserId)
            throw new UnauthorizedAccessException("You are not allowed to access this invoice.");
        return ToInvoiceDTO(invoice);
    }

    public async Task<double> GetTotalRevenueAsync(DateTime from, DateTime to)
        => await _uow.InvoiceStore.GetTotalRevenueAsync(from, to);

    public async Task<List<RevenueByDayDTO>> GetRevenueByDayAsync(DateTime from, DateTime to)
    {
        var map = await _uow.InvoiceStore.GetRevenueByDayAsync(from, to);
        var result = new List<RevenueByDayDTO>();
        for (var day = from.Date; day <= to.Date; day = day.AddDays(1))
            result.Add(new RevenueByDayDTO { Date = day, Total = map.TryGetValue(day, out var t) ? t : 0 });
        return result;
    }

    public async Task<List<RevenueBreakdownDTO>> GetRevenueByMovieAsync(DateTime from, DateTime to)
    {
        var map = await _uow.InvoiceStore.GetRevenueByMovieAsync(from, to);
        return map.Select(kv => new RevenueBreakdownDTO { Name = kv.Key, Total = kv.Value })
                  .OrderByDescending(x => x.Total).ToList();
    }

    public async Task<List<RevenueBreakdownDTO>> GetRevenueByTheaterAsync(DateTime from, DateTime to)
    {
        var map = await _uow.InvoiceStore.GetRevenueByTheaterAsync(from, to);
        return map.Select(kv => new RevenueBreakdownDTO { Name = kv.Key, Total = kv.Value })
                  .OrderByDescending(x => x.Total).ToList();
    }

    private static InvoiceDTO ToInvoiceDTO(Invoice invoice)
    {
        var dto = invoice.ToDTO<Invoice, InvoiceDTO>();
        dto.UserName  = invoice.User?.Name  ?? string.Empty;
        dto.UserEmail = invoice.User?.Email ?? string.Empty;
        dto.Tickets   = invoice.InvoiceTickets?.Select(ToInvoiceTicketDTO).ToList() ?? [];
        dto.Foods     = invoice.InvoiceFoodAndDrinks?.Select(ToInvoiceFoodDTO).ToList() ?? [];
        return dto;
    }

    private static InvoiceTicketDTO ToInvoiceTicketDTO(InvoiceTicket ticket)
    {
        var dto = ticket.ToDTO<InvoiceTicket, InvoiceTicketDTO>();
        dto.MovieTitle  = ticket.ShowTimeRoom?.ShowTime?.Movie?.Title ?? string.Empty;
        dto.TheaterName = ticket.ShowTimeRoom?.Room?.Theater?.Name    ?? string.Empty;
        dto.RoomName    = ticket.ShowTimeRoom?.Room?.Name             ?? string.Empty;
        dto.ShowTime    = ticket.ShowTimeRoom?.ShowTime?.StartTime    ?? default;
        dto.SeatLabel   = ticket.Seat != null ? $"{ticket.Seat.RowName}{ticket.Seat.ColIndex}" : string.Empty;
        dto.SeatType    = ticket.Seat?.SeatType?.Name ?? string.Empty;
        return dto;
    }

    private static InvoiceFoodDTO ToInvoiceFoodDTO(InvoiceFoodAndDrink food)
    {
        var dto = food.ToDTO<InvoiceFoodAndDrink, InvoiceFoodDTO>();
        dto.FoodName = food.FoodAndDrink?.Name ?? string.Empty;
        return dto;
    }
}
