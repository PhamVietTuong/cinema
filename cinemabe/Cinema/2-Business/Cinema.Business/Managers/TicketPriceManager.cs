using Cinema.Business.Contracts;
using Cinema.Business.DTO.Catalog;
using Cinema.Business.DTO.Requests;
using Cinema.Business.Extensions;
using Cinema.Business.Helpers;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Microsoft.EntityFrameworkCore;

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

    // No free-text field on this entity — filters are all Guid foreign keys + the day of week.
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

                case "timeSlotId":
                    if (Guid.TryParse(filters[key], out var timeSlotId))
                    {
                        query = _uow.TicketPriceStore.FilterQuery(query, e => e.TimeSlotId == timeSlotId);
                    }
                    break;

                case "dayOfWeek":
                    if (Enum.TryParse<DayOfWeek>(filters[key], out var dayOfWeek))
                    {
                        query = _uow.TicketPriceStore.FilterQuery(query, e => e.DayOfWeek == dayOfWeek);
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
            "dayOfWeek" => _uow.TicketPriceStore.OrderQuery(query, e => e.DayOfWeek, sort.Ascending),
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

    private async Task ValidateScopeAsync(Guid theaterId, Guid roomTypeId, Guid timeSlotId)
    {
        if (!await _uow.RoomTypeStore.ExistsAsync(r => r.Id == roomTypeId && r.TheaterId == theaterId))
        {
            throw new InvalidOperationException("RoomType does not belong to this theater.");
        }

        if (!await _uow.TimeSlotStore.ExistsAsync(t => t.Id == timeSlotId && t.TheaterId == theaterId))
        {
            throw new InvalidOperationException("TimeSlot does not belong to this theater.");
        }
    }

    private Task<bool> DuplicateExistsAsync(Guid theaterId, Guid roomTypeId, Guid timeSlotId, DayOfWeek dayOfWeek, Guid? excludeId)
    {
        return _uow.TicketPriceStore.ExistsAsync(e =>
            e.TheaterId == theaterId && e.RoomTypeId == roomTypeId && e.TimeSlotId == timeSlotId &&
            e.DayOfWeek == dayOfWeek && (excludeId == null || e.Id != excludeId));
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
        if (!Enum.IsDefined(request.DayOfWeek))
        {
            throw new InvalidOperationException("Invalid day of week.");
        }
        await ValidateScopeAsync(request.TheaterId, request.RoomTypeId, request.TimeSlotId);
        if (await DuplicateExistsAsync(request.TheaterId, request.RoomTypeId, request.TimeSlotId, request.DayOfWeek, null))
        {
            throw new InvalidOperationException("A ticket price already exists for this room type, time slot and day.");
        }

        var entity = request.ToNewEntity<CreateTicketPriceRequest, TicketPrice>();
        await _uow.TicketPriceStore.CreateAsync(entity);
        return entity.ToDTO<TicketPrice, TicketPriceDTO>();
    }

    public async Task<TicketPriceDTO> UpdateAsync(UpdateTicketPriceRequest request)
    {
        ValidateMultiplier(request.PriceMultiplier);
        if (!Enum.IsDefined(request.DayOfWeek))
        {
            throw new InvalidOperationException("Invalid day of week.");
        }
        var entity = await _uow.TicketPriceStore.GetByIdAsync(request.Id);
        if (entity == null)
        {
            throw new KeyNotFoundException($"TicketPrice {request.Id} not found.");
        }
        await ValidateScopeAsync(request.TheaterId, request.RoomTypeId, request.TimeSlotId);
        if (await DuplicateExistsAsync(request.TheaterId, request.RoomTypeId, request.TimeSlotId, request.DayOfWeek, request.Id))
        {
            throw new InvalidOperationException("A ticket price already exists for this room type, time slot and day.");
        }

        entity.PatchEntity<TicketPrice, UpdateTicketPriceRequest>(request);
        await _uow.TicketPriceStore.UpdateAsync(entity);
        return entity.ToDTO<TicketPrice, TicketPriceDTO>();
    }

    public async Task DeleteAsync(Guid id)
    {
        await _uow.TicketPriceStore.DeleteAsync(id);
    }

    public async Task<List<TicketPriceDTO>> UpsertAsync(UpsertTicketPricesRequest request)
    {
        ValidateMultiplier(request.PriceMultiplier);

        var days = (request.DaysOfWeek ?? new List<DayOfWeek>()).Distinct().ToList();
        if (days.Count == 0)
        {
            throw new InvalidOperationException("At least one day of week is required.");
        }
        if (days.Any(d => !Enum.IsDefined(d)))
        {
            throw new InvalidOperationException("Invalid day of week.");
        }

        await ValidateScopeAsync(request.TheaterId, request.RoomTypeId, request.TimeSlotId);

        await _uow.BeginTransactionAsync();
        try
        {
            var existing = (await _uow.TicketPriceStore.FindAllAsync(e =>
                e.TheaterId == request.TheaterId && e.RoomTypeId == request.RoomTypeId &&
                e.TimeSlotId == request.TimeSlotId && days.Contains(e.DayOfWeek))).ToList();

            foreach (var entity in existing)
            {
                entity.PriceMultiplier = request.PriceMultiplier;
            }
            if (existing.Count > 0)
            {
                await _uow.TicketPriceStore.UpdateRangeAsync(existing);
            }

            var missingDays = days.Except(existing.Select(e => e.DayOfWeek)).ToList();
            var created = missingDays.Select(d => new TicketPrice
            {
                TheaterId = request.TheaterId,
                RoomTypeId = request.RoomTypeId,
                TimeSlotId = request.TimeSlotId,
                DayOfWeek = d,
                PriceMultiplier = request.PriceMultiplier,
            }).ToList();
            if (created.Count > 0)
            {
                await _uow.TicketPriceStore.CreateRangeAsync(created);
            }

            await _uow.CommitTransactionAsync();
            return existing.Concat(created).Select(e => e.ToDTO<TicketPrice, TicketPriceDTO>()).ToList();
        }
        catch (DbUpdateException)
        {
            // Another request inserted one of these days between our existence check and this commit
            // (the unique index is the source of truth, not the pre-check) — surface the same
            // translated message a single Create/Update duplicate would give, not a raw 500.
            await _uow.RollbackTransactionAsync();
            throw new InvalidOperationException("A ticket price already exists for this room type, time slot and day.");
        }
        catch
        {
            await _uow.RollbackTransactionAsync();
            throw;
        }
    }
}
