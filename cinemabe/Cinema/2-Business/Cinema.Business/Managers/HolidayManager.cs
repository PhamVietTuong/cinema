using Cinema.Business.Contracts;
using Cinema.Business.DTO.Catalog;
using Cinema.Business.DTO.Requests;
using Cinema.Business.Extensions;
using Cinema.Business.Helpers;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Cinema.Business.Managers;

public class HolidayManager : IHolidayManager
{
    protected readonly IApplicationUnitOfWork _uow;

    public HolidayManager(IApplicationUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await _uow.HolidayStore.ExistsAsync(e => e.Id == id);
    }

    private IQueryable<Holiday> GetFilteredHolidayQuery(Dictionary<string, string>? filters)
    {
        var query = _uow.HolidayStore.GetQuery();
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
                case FilterKeys.Keyword:
                    var keyword = filters[key];
                    query = _uow.HolidayStore.FilterQuery(query, e => e.Name.Contains(keyword));
                    break;

                case "name":
                    var name = filters[key];
                    query = _uow.HolidayStore.FilterQuery(query, e => e.Name.Contains(name));
                    break;

                case "date":
                    if (DateOnly.TryParse(filters[key], out var date))
                    {
                        query = _uow.HolidayStore.FilterQuery(query, e => e.Date == date);
                    }
                    break;

                case "priceMultiplier":
                    if (double.TryParse(filters[key], out var priceMultiplier))
                    {
                        query = _uow.HolidayStore.FilterQuery(query, e => e.PriceMultiplier == priceMultiplier);
                    }
                    break;

                case "dateFrom":
                    if (DateOnly.TryParse(filters[key], out var dateFrom))
                    {
                        query = _uow.HolidayStore.FilterQuery(query, e => e.Date >= dateFrom);
                    }
                    break;
            }
        }
        return query;
    }

    private IQueryable<Holiday> ApplySort(IQueryable<Holiday> query, SortDTO? sort)
    {
        if (sort == null || string.IsNullOrEmpty(sort.Field))
        {
            return query;
        }

        return sort.Field switch
        {
            "name" => _uow.HolidayStore.OrderQuery(query, e => e.Name, sort.Ascending),
            "date" => _uow.HolidayStore.OrderQuery(query, e => e.Date, sort.Ascending),
            "priceMultiplier" => _uow.HolidayStore.OrderQuery(query, e => e.PriceMultiplier, sort.Ascending),
            _ => query,
        };
    }

    public async Task<DefaultSearchResults<HolidayDTO>> GetAsync(PagingSearchDTO search)
    {
        search ??= new PagingSearchDTO();
        var (page, pageSize) = PagingHelper.ResolvePaging(search);

        var query = GetFilteredHolidayQuery(search.Filters);
        query = ApplySort(query, search.Sort);
        var total = await _uow.HolidayStore.CountAsync(query);
        var items = await _uow.HolidayStore.AllPageAsync(query, page - 1, pageSize);
        return PagingHelper.ToPagedResult<Holiday, HolidayDTO>(items, total, page, pageSize);
    }

    public async Task<HolidayDTO> GetByIdAsync(Guid id)
    {
        var entity = await _uow.HolidayStore.GetByIdAsync(id);
        if (entity == null)
        {
            throw new KeyNotFoundException($"Holiday {id} not found.");
        }
        return entity.ToDTO<Holiday, HolidayDTO>();
    }

    public async Task<HolidayDTO> CreateAsync(CreateHolidayRequest request)
    {
        var entity = request.ToNewEntity<CreateHolidayRequest, Holiday>();
        await _uow.HolidayStore.CreateAsync(entity);
        return entity.ToDTO<Holiday, HolidayDTO>();
    }

    public async Task<HolidayDTO> UpdateAsync(UpdateHolidayRequest request)
    {
        var entity = await _uow.HolidayStore.GetByIdAsync(request.Id);
        if (entity == null)
        {
            throw new KeyNotFoundException($"Holiday {request.Id} not found.");
        }
        entity.PatchEntity<Holiday, UpdateHolidayRequest>(request);
        await _uow.HolidayStore.UpdateAsync(entity);
        return entity.ToDTO<Holiday, HolidayDTO>();
    }

    public async Task DeleteAsync(Guid id)
    {
        // The per-slot overrides go with the holiday through the FK's ON DELETE CASCADE.
        await _uow.HolidayStore.DeleteAsync(id);
    }

    public async Task<DefaultSearchResults<HolidayTimeSlotPriceDTO>> GetTimeSlotPricesAsync(PagingSearchDTO search)
    {
        search ??= new PagingSearchDTO();
        var (page, pageSize) = PagingHelper.ResolvePaging(search);

        var query = _uow.HolidayTimeSlotPriceStore.GetQuery();
        if (search.Filters != null)
        {
            if (search.Filters.TryGetValue("holidayId", out var holidayRaw) && Guid.TryParse(holidayRaw, out var holidayId))
            {
                query = _uow.HolidayTimeSlotPriceStore.FilterQuery(query, e => e.HolidayId == holidayId);
            }
            if (search.Filters.TryGetValue(FilterKeys.TheaterId, out var theaterRaw) && Guid.TryParse(theaterRaw, out var theaterId))
            {
                query = _uow.HolidayTimeSlotPriceStore.FilterQuery(query, e => e.TimeSlot.TheaterId == theaterId);
            }
        }

        var total = await _uow.HolidayTimeSlotPriceStore.CountAsync(query);
        var items = await _uow.HolidayTimeSlotPriceStore.AllPageAsync(query, page - 1, pageSize);
        return PagingHelper.ToPagedResult<HolidayTimeSlotPrice, HolidayTimeSlotPriceDTO>(items, total, page, pageSize);
    }

    public async Task<List<HolidayTimeSlotPriceDTO>> UpsertTimeSlotPricesAsync(UpsertHolidayTimeSlotPricesRequest request)
    {
        var items = request.Items ?? new List<HolidayTimeSlotPriceItem>();
        if (items.GroupBy(i => i.TimeSlotId).Any(g => g.Count() > 1))
        {
            throw new InvalidOperationException("A time slot can only be listed once.");
        }
        if (items.Any(i => i.PriceMultiplier is <= 0))
        {
            throw new InvalidOperationException($"{nameof(HolidayTimeSlotPrice.PriceMultiplier)} must be greater than 0.");
        }
        if (!await _uow.HolidayStore.ExistsAsync(h => h.Id == request.HolidayId))
        {
            throw new KeyNotFoundException($"Holiday {request.HolidayId} not found.");
        }

        // Every listed slot must belong to the named theater, so one theater's editor can never
        // write another theater's prices. One query for all slots, not one per item.
        var slotIds = items.Select(i => i.TimeSlotId).ToList();
        var validSlotCount = await _uow.TimeSlotStore.CountAsync(t => t.TheaterId == request.TheaterId && slotIds.Contains(t.Id));
        if (validSlotCount != slotIds.Count)
        {
            throw new InvalidOperationException("TimeSlot does not belong to this theater.");
        }

        await _uow.BeginTransactionAsync();
        try
        {
            var existing = (await _uow.HolidayTimeSlotPriceStore.FindAllAsync(e =>
                e.HolidayId == request.HolidayId && slotIds.Contains(e.TimeSlotId))).ToList();
            var existingBySlot = existing.ToDictionary(e => e.TimeSlotId);

            var toCreate = new List<HolidayTimeSlotPrice>();
            var toUpdate = new List<HolidayTimeSlotPrice>();
            foreach (var item in items)
            {
                existingBySlot.TryGetValue(item.TimeSlotId, out var row);
                if (item.PriceMultiplier is not double multiplier)
                {
                    if (row is not null)
                    {
                        await _uow.HolidayTimeSlotPriceStore.DeleteAsync(row);
                        existing.Remove(row);
                    }
                    continue;
                }
                if (row is null)
                {
                    toCreate.Add(new HolidayTimeSlotPrice
                    {
                        HolidayId = request.HolidayId,
                        TimeSlotId = item.TimeSlotId,
                        PriceMultiplier = multiplier,
                    });
                }
                else
                {
                    row.PriceMultiplier = multiplier;
                    toUpdate.Add(row);
                }
            }
            if (toUpdate.Count > 0)
            {
                await _uow.HolidayTimeSlotPriceStore.UpdateRangeAsync(toUpdate);
            }
            if (toCreate.Count > 0)
            {
                await _uow.HolidayTimeSlotPriceStore.CreateRangeAsync(toCreate);
            }

            await _uow.CommitTransactionAsync();
            return existing.Concat(toCreate)
                .Select(e => e.ToDTO<HolidayTimeSlotPrice, HolidayTimeSlotPriceDTO>())
                .ToList();
        }
        catch (DbUpdateException)
        {
            // A concurrent save inserted one of these slots first — the unique index is the source of
            // truth, so surface a translated message instead of a raw 500.
            await _uow.RollbackTransactionAsync();
            throw new InvalidOperationException("A holiday price already exists for this time slot.");
        }
        catch
        {
            await _uow.RollbackTransactionAsync();
            throw;
        }
    }
}
