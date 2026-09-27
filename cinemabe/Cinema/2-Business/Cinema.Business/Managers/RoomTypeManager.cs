using Cinema.Business.Contracts;
using Cinema.Business.DTO.Catalog;
using Cinema.Business.DTO.Requests;
using Cinema.Business.Extensions;
using Cinema.Business.Helpers;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;

namespace Cinema.Business.Managers;

public class RoomTypeManager : IRoomTypeManager
{
    protected readonly IApplicationUnitOfWork _uow;

    public RoomTypeManager(IApplicationUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await _uow.RoomTypeStore.ExistsAsync(e => e.Id == id);
    }

    private IQueryable<RoomType> GetFilteredRoomTypeQuery(Dictionary<string, string>? filters)
    {
        var query = _uow.RoomTypeStore.GetQuery();
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
                    query = _uow.RoomTypeStore.FilterQuery(query, e => e.Name.Contains(keyword));
                    break;

                case "theaterId":
                    if (Guid.TryParse(filters[key], out var theaterId))
                    {
                        query = _uow.RoomTypeStore.FilterQuery(query, e => e.TheaterId == theaterId);
                    }
                    break;
            }
        }
        return query;
    }

    private IQueryable<RoomType> ApplySort(IQueryable<RoomType> query, SortDTO? sort)
    {
        if (sort == null || string.IsNullOrEmpty(sort.Field))
        {
            return query;
        }

        return sort.Field switch
        {
            "name" => _uow.RoomTypeStore.OrderQuery(query, e => e.Name, sort.Ascending),
            _ => query,
        };
    }

    public async Task<DefaultSearchResults<RoomTypeDTO>> GetAsync(PagingSearchDTO search)
    {
        search ??= new PagingSearchDTO();
        var (page, pageSize) = PagingHelper.ResolvePaging(search);

        var query = GetFilteredRoomTypeQuery(search.Filters);
        query = ApplySort(query, search.Sort);
        var total = await _uow.RoomTypeStore.CountAsync(query);
        var items = await _uow.RoomTypeStore.AllPageAsync(query, page - 1, pageSize);
        return PagingHelper.ToPagedResult<RoomType, RoomTypeDTO>(items, total, page, pageSize);
    }

    public async Task<RoomTypeDTO> GetByIdAsync(Guid id)
    {
        var entity = await _uow.RoomTypeStore.GetByIdAsync(id);
        if (entity == null)
        {
            throw new KeyNotFoundException($"RoomType {id} not found.");
        }
        return entity.ToDTO<RoomType, RoomTypeDTO>();
    }

    public async Task<RoomTypeDTO> CreateAsync(CreateRoomTypeRequest request)
    {
        ValidateTurnoverBuffer(request.TurnoverBufferMinutes);
        var entity = request.ToNewEntity<CreateRoomTypeRequest, RoomType>();

        // A brand-new RoomType starts with every active theater-wide category included at its
        // default price, so it's bookable immediately — the admin then removes what shouldn't
        // be offered here, rather than starting from a fully unbookable room type.
        var categories = await _uow.PatronCategoryStore.FindAsync(c => c.TheaterId == request.TheaterId && c.IsActive);

        await _uow.BeginTransactionAsync();
        try
        {
            await _uow.RoomTypeStore.CreateAsync(entity);
            foreach (var category in categories)
            {
                await _uow.RoomTypePatronCategoryPriceStore.CreateAsync(new RoomTypePatronCategoryPrice
                {
                    RoomTypeId       = entity.Id,
                    PatronCategoryId = category.Id,
                    Price            = category.Price,
                });
            }
            await _uow.CommitTransactionAsync();
        }
        catch
        {
            await _uow.RollbackTransactionAsync();
            throw;
        }

        return entity.ToDTO<RoomType, RoomTypeDTO>();
    }

    public async Task<RoomTypeDTO> UpdateAsync(UpdateRoomTypeRequest request)
    {
        ValidateTurnoverBuffer(request.TurnoverBufferMinutes);
        var entity = await _uow.RoomTypeStore.GetByIdAsync(request.Id);
        if (entity == null)
        {
            throw new KeyNotFoundException($"RoomType {request.Id} not found.");
        }
        entity.PatchEntity<RoomType, UpdateRoomTypeRequest>(request);
        await _uow.RoomTypeStore.UpdateAsync(entity);
        return entity.ToDTO<RoomType, RoomTypeDTO>();
    }

    public async Task DeleteAsync(Guid id)
    {
        // RoomTypePatronCategoryPrice's FK to RoomType is NO ACTION (both parents already cascade
        // from Theater, so only one leg may cascade) — clean up its override rows first.
        await _uow.BeginTransactionAsync();
        try
        {
            await _uow.RoomTypePatronCategoryPriceStore.DeleteByRoomTypeAsync(id);
            await _uow.RoomTypeStore.DeleteAsync(id);
            await _uow.CommitTransactionAsync();
        }
        catch
        {
            await _uow.RollbackTransactionAsync();
            throw;
        }
    }

    private const int MaxTurnoverBufferMinutes = 240;

    /// <summary>
    /// Rejects a nonsensical buffer before it reaches the room-overlap check: a negative value
    /// would shrink the exclusion window and let real overlaps through, and an absurdly large one
    /// would block scheduling for the whole day. The Angular form's min validator alone doesn't
    /// stop a direct API call.
    /// </summary>
    private static void ValidateTurnoverBuffer(int turnoverBufferMinutes)
    {
        if (turnoverBufferMinutes < 0 || turnoverBufferMinutes > MaxTurnoverBufferMinutes)
        {
            throw new InvalidOperationException(
                $"{nameof(RoomType.TurnoverBufferMinutes)} must be between 0 and {MaxTurnoverBufferMinutes} minutes.");
        }
    }
}
