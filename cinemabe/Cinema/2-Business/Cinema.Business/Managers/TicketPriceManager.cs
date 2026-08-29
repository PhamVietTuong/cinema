using System.Linq.Expressions;
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

    public async Task<DefaultSearchResults<TicketPriceDTO>> GetAsync(PagingSearchDTO search)
    {
        search ??= new PagingSearchDTO();
        var (page, pageSize) = PagingHelper.ResolvePaging(search);

        // No free-text field on this entity — filters are all Guid foreign keys + the holiday flag.
        var theaterId = search.Filters.GetGuid("theaterId");
        var roomTypeId = search.Filters.GetGuid("roomTypeId");
        var seatTypeId = search.Filters.GetGuid("seatTypeId");
        var timeSlotId = search.Filters.GetGuid("timeSlotId");
        var isHoliday = search.Filters.GetBool("isHoliday");

        Expression<Func<TicketPrice, bool>> predicate = e =>
            (theaterId == null || e.TheaterId == theaterId) &&
            (roomTypeId == null || e.RoomTypeId == roomTypeId) &&
            (seatTypeId == null || e.SeatTypeId == seatTypeId) &&
            (timeSlotId == null || e.TimeSlotId == timeSlotId) &&
            (isHoliday == null || e.IsHoliday == isHoliday);

        var total = await _uow.TicketPriceStore.CountAsync(predicate);
        var items = await _uow.TicketPriceStore.FindAllPageAsync(page - 1, pageSize, predicate);
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
        var entity = request.ToNewEntity<CreateTicketPriceRequest, TicketPrice>();
        await _uow.TicketPriceStore.CreateAsync(entity);
        return entity.ToDTO<TicketPrice, TicketPriceDTO>();
    }

    public async Task<TicketPriceDTO> UpdateAsync(UpdateTicketPriceRequest request)
    {
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
