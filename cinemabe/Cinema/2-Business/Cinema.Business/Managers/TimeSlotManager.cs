using System.Linq.Expressions;
using Cinema.Business.Contracts;
using Cinema.Business.DTO.Catalog;
using Cinema.Business.DTO.Requests;
using Cinema.Business.Extensions;
using Cinema.Business.Helpers;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;

namespace Cinema.Business.Managers;

public class TimeSlotManager(IApplicationUnitOfWork uow)
    : ITimeSlotManager
{
    protected readonly IApplicationUnitOfWork _uow = uow;

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await _uow.TimeSlotStore.ExistsAsync(e => e.Id == id);
    }

    public async Task<DefaultSearchResults<TimeSlotDTO>> GetAsync(PagingSearchDTO search)
    {
        search ??= new PagingSearchDTO();
        var (page, pageSize) = PagingHelper.ResolvePaging(search);
        var keyword = search.Filters.GetString("keyword");
        var theaterId = search.Filters.GetGuid("theaterId");

        Expression<Func<TimeSlot, bool>> predicate = e =>
            (string.IsNullOrEmpty(keyword) || e.Name.Contains(keyword!)) &&
            (theaterId == null || e.TheaterId == theaterId);

        var total = await _uow.TimeSlotStore.CountAsync(predicate);
        var items = await _uow.TimeSlotStore.FindAllPageAsync(page - 1, pageSize, predicate);
        return PagingHelper.ToPagedResult<TimeSlot, TimeSlotDTO>(items, total, page, pageSize);
    }

    public async Task<TimeSlotDTO> GetByIdAsync(Guid id)
    {
        var entity = await _uow.TimeSlotStore.GetByIdAsync(id);
        if (entity == null)
        {
            throw new KeyNotFoundException($"TimeSlot {id} not found.");
        }
        return entity.ToDTO<TimeSlot, TimeSlotDTO>();
    }

    public async Task<TimeSlotDTO> CreateAsync(CreateTimeSlotRequest request)
    {
        var entity = request.ToNewEntity<CreateTimeSlotRequest, TimeSlot>();
        await _uow.TimeSlotStore.CreateAsync(entity);
        return entity.ToDTO<TimeSlot, TimeSlotDTO>();
    }

    public async Task<TimeSlotDTO> UpdateAsync(UpdateTimeSlotRequest request)
    {
        var entity = await _uow.TimeSlotStore.GetByIdAsync(request.Id);
        if (entity == null)
        {
            throw new KeyNotFoundException($"TimeSlot {request.Id} not found.");
        }
        entity.PatchEntity<TimeSlot, UpdateTimeSlotRequest>(request);
        await _uow.TimeSlotStore.UpdateAsync(entity);
        return entity.ToDTO<TimeSlot, TimeSlotDTO>();
    }

    public async Task DeleteAsync(Guid id)
    {
        await _uow.TimeSlotStore.DeleteAsync(id);
    }
}
