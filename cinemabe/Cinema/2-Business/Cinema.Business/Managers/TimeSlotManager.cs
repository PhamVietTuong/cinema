using Cinema.Business.Contracts;
using Cinema.Business.DTO.Catalog;
using Cinema.Business.DTO.Requests;
using Cinema.Business.Extensions;
using Cinema.Business.Helpers;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;

namespace Cinema.Business.Managers;

public class TimeSlotManager : ITimeSlotManager
{
    protected readonly IApplicationUnitOfWork _uow;

    public TimeSlotManager(IApplicationUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await _uow.TimeSlotStore.ExistsAsync(e => e.Id == id);
    }

    private IQueryable<TimeSlot> GetFilteredTimeSlotQuery(Dictionary<string, string>? filters)
    {
        var query = _uow.TimeSlotStore.GetQuery();
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
                case "keyword":
                    var keyword = filters[key];
                    query = _uow.TimeSlotStore.FilterQuery(query, e => e.Name.Contains(keyword));
                    break;

                case "theaterId":
                    if (Guid.TryParse(filters[key], out var theaterId))
                    {
                        query = _uow.TimeSlotStore.FilterQuery(query, e => e.TheaterId == theaterId);
                    }
                    break;
            }
        }
        return query;
    }

    private IQueryable<TimeSlot> ApplySort(IQueryable<TimeSlot> query, SortDTO? sort)
    {
        if (sort == null || string.IsNullOrEmpty(sort.Field))
        {
            return query;
        }

        return sort.Field switch
        {
            "name" => _uow.TimeSlotStore.OrderQuery(query, e => e.Name, sort.Ascending),
            _ => query,
        };
    }

    public async Task<DefaultSearchResults<TimeSlotDTO>> GetAsync(PagingSearchDTO search)
    {
        search ??= new PagingSearchDTO();
        var (page, pageSize) = PagingHelper.ResolvePaging(search);

        var query = GetFilteredTimeSlotQuery(search.Filters);
        query = ApplySort(query, search.Sort);
        var total = await _uow.TimeSlotStore.CountAsync(query);
        var items = await _uow.TimeSlotStore.AllPageAsync(query, page - 1, pageSize);
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
