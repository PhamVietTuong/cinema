using Cinema.Business.Contracts;
using Cinema.Business.DTO.Catalog;
using Cinema.Business.DTO.Requests;
using Cinema.Business.Extensions;
using Cinema.Business.Helpers;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;

namespace Cinema.Business.Managers;

public class TicketPriceManager : ITicketPriceManager
{
    protected readonly IApplicationUnitOfWork _uow;

    public TicketPriceManager(IApplicationUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await _uow.TicketPriceStore.ExistsAsync(e => e.Id == id);
    }

    // No free-text field on this entity — filters are all Guid foreign keys + the holiday flag.
    private IQueryable<TicketPrice> GetFilteredTicketPriceQuery(Dictionary<string, string>? filters)
    {
        var query = _uow.TicketPriceStore.GetQuery();
        if (filters == null)
        {
            return query;
        }

        foreach (var key in filters.Keys)
        {
            if (string.IsNullOrEmpty(filters[key]))
            {
                continue;
            }

            switch (key)
            {
                case "theaterId":
                    if (Guid.TryParse(filters[key], out var theaterId))
                    {
                        query = _uow.TicketPriceStore.FilterQuery(query, e => e.TheaterId == theaterId);
                    }
                    break;

                case "roomTypeId":
                    if (Guid.TryParse(filters[key], out var roomTypeId))
                    {
                        query = _uow.TicketPriceStore.FilterQuery(query, e => e.RoomTypeId == roomTypeId);
                    }
                    break;

                case "seatTypeId":
                    if (Guid.TryParse(filters[key], out var seatTypeId))
                    {
                        query = _uow.TicketPriceStore.FilterQuery(query, e => e.SeatTypeId == seatTypeId);
                    }
                    break;

                case "timeSlotId":
                    if (Guid.TryParse(filters[key], out var timeSlotId))
                    {
                        query = _uow.TicketPriceStore.FilterQuery(query, e => e.TimeSlotId == timeSlotId);
                    }
                    break;

                case "isHoliday":
                    if (bool.TryParse(filters[key], out var isHoliday))
                    {
                        query = _uow.TicketPriceStore.FilterQuery(query, e => e.IsHoliday == isHoliday);
                    }
                    break;
            }
        }
        return query;
    }

    private IQueryable<TicketPrice> ApplySort(IQueryable<TicketPrice> query, SortDTO? sort)
    {
        if (sort == null || string.IsNullOrEmpty(sort.Field))
        {
            return query;
        }

        return sort.Field switch
        {
            "isHoliday" => _uow.TicketPriceStore.OrderQuery(query, e => e.IsHoliday, sort.Ascending),
            "priceMultiplier" => _uow.TicketPriceStore.OrderQuery(query, e => e.PriceMultiplier, sort.Ascending),
            _ => query,
        };
    }

    private static void ValidateMultiplier(double priceMultiplier)
    {
        if (priceMultiplier <= 0)
        {
            throw new InvalidOperationException($"{nameof(TicketPrice.PriceMultiplier)} must be greater than 0.");
        }
    }

    public async Task<DefaultSearchResults<TicketPriceDTO>> GetAsync(PagingSearchDTO search)
    {
        search ??= new PagingSearchDTO();
        var (page, pageSize) = PagingHelper.ResolvePaging(search);

        var query = GetFilteredTicketPriceQuery(search.Filters);
        query = ApplySort(query, search.Sort);
        var total = await _uow.TicketPriceStore.CountAsync(query);
        var items = await _uow.TicketPriceStore.AllPageAsync(query, page - 1, pageSize);
        return PagingHelper.ToPagedResult<TicketPrice, TicketPriceDTO>(items, total, page, pageSize);
    }

    public async Task<TicketPriceDTO> GetByIdAsync(Guid id)
    {
        var entity = await _uow.TicketPriceStore.GetByIdAsync(id);
        if (entity == null)
        {
            throw new KeyNotFoundException($"TicketPrice {id} not found.");
        }
        return entity.ToDTO<TicketPrice, TicketPriceDTO>();
    }

    public async Task<TicketPriceDTO> CreateAsync(CreateTicketPriceRequest request)
    {
        ValidateMultiplier(request.PriceMultiplier);
        var entity = request.ToNewEntity<CreateTicketPriceRequest, TicketPrice>();
        await _uow.TicketPriceStore.CreateAsync(entity);
        return entity.ToDTO<TicketPrice, TicketPriceDTO>();
    }

    public async Task<TicketPriceDTO> UpdateAsync(UpdateTicketPriceRequest request)
    {
        ValidateMultiplier(request.PriceMultiplier);
        var entity = await _uow.TicketPriceStore.GetByIdAsync(request.Id);
        if (entity == null)
        {
            throw new KeyNotFoundException($"TicketPrice {request.Id} not found.");
        }
        entity.PatchEntity<TicketPrice, UpdateTicketPriceRequest>(request);
        await _uow.TicketPriceStore.UpdateAsync(entity);
        return entity.ToDTO<TicketPrice, TicketPriceDTO>();
    }

    public async Task DeleteAsync(Guid id)
    {
        await _uow.TicketPriceStore.DeleteAsync(id);
    }
}
