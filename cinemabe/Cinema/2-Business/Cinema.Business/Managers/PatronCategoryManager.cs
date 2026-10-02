using Cinema.Business.Contracts;
using Cinema.Business.DTO.Catalog;
using Cinema.Business.DTO.Requests;
using Cinema.Business.Extensions;
using Cinema.Business.Helpers;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;

namespace Cinema.Business.Managers;

public class PatronCategoryManager : IPatronCategoryManager
{
    protected readonly IApplicationUnitOfWork _uow;

    public PatronCategoryManager(IApplicationUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await _uow.PatronCategoryStore.ExistsAsync(e => e.Id == id);
    }

    private IQueryable<PatronCategory> GetFilteredPatronCategoryQuery(Dictionary<string, string>? filters)
    {
        var query = _uow.PatronCategoryStore.GetQuery();
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
                    query = _uow.PatronCategoryStore.FilterQuery(query, e => e.Name.Contains(keyword));
                    break;

                case "theaterId":
                    if (Guid.TryParse(filters[key], out var theaterId))
                    {
                        query = _uow.PatronCategoryStore.FilterQuery(query, e => e.TheaterId == theaterId);
                    }
                    break;

                case "isActive":
                    if (bool.TryParse(filters[key], out var isActive))
                    {
                        query = _uow.PatronCategoryStore.FilterQuery(query, e => e.IsActive == isActive);
                    }
                    break;
            }
        }
        return query;
    }

    private IQueryable<PatronCategory> ApplySort(IQueryable<PatronCategory> query, SortDTO? sort)
    {
        if (sort == null || string.IsNullOrEmpty(sort.Field))
        {
            return query;
        }

        return sort.Field switch
        {
            "name"  => _uow.PatronCategoryStore.OrderQuery(query, e => e.Name, sort.Ascending),
            "price" => _uow.PatronCategoryStore.OrderQuery(query, e => e.Price, sort.Ascending),
            _ => query,
        };
    }

    public async Task<DefaultSearchResults<PatronCategoryDTO>> GetAsync(PagingSearchDTO search)
    {
        search ??= new PagingSearchDTO();
        var (page, pageSize) = PagingHelper.ResolvePaging(search);

        var query = GetFilteredPatronCategoryQuery(search.Filters);
        query = ApplySort(query, search.Sort);
        var total = await _uow.PatronCategoryStore.CountAsync(query);
        var items = await _uow.PatronCategoryStore.AllPageAsync(query, page - 1, pageSize);
        var result = PagingHelper.ToPagedResult<PatronCategory, PatronCategoryDTO>(items, total, page, pageSize);
        await AttachSeatTypeInfoAsync(result.Results);
        return result;
    }

    public async Task<PatronCategoryDTO> GetByIdAsync(Guid id)
    {
        var entity = await _uow.PatronCategoryStore.GetByIdAsync(id);
        if (entity == null)
        {
            throw new KeyNotFoundException($"PatronCategory {id} not found.");
        }
        var dto = entity.ToDTO<PatronCategory, PatronCategoryDTO>();
        await AttachSeatTypeInfoAsync(new[] { dto });
        return dto;
    }

    public async Task<List<PatronCategoryDTO>> GetByTheaterAsync(Guid theaterId)
    {
        var entities = (await _uow.PatronCategoryStore.FindAsync(c => c.TheaterId == theaterId)).ToList();
        var dtos = entities.Select(e => e.ToDTO<PatronCategory, PatronCategoryDTO>()).ToList();
        await AttachSeatTypeInfoAsync(dtos);
        return dtos;
    }

    public async Task<PatronCategoryDTO> CreateAsync(CreatePatronCategoryRequest request)
    {
        await ValidateSeatTypeAsync(request.TheaterId, request.SeatTypeId);
        await EnsureNotDuplicateAsync(request.TheaterId, request.Name, request.SeatTypeId, excludeId: null);

        var entity = new PatronCategory
        {
            TheaterId   = request.TheaterId,
            SeatTypeId  = request.SeatTypeId,
            Name        = request.Name,
            Description = request.Description,
            Price       = request.Price,
            IsActive    = request.IsActive,
        };
        await _uow.PatronCategoryStore.CreateAsync(entity);

        var dto = entity.ToDTO<PatronCategory, PatronCategoryDTO>();
        await AttachSeatTypeInfoAsync(new[] { dto });
        return dto;
    }

    public async Task<PatronCategoryDTO> UpdateAsync(UpdatePatronCategoryRequest request)
    {
        var entity = await _uow.PatronCategoryStore.GetByIdAsync(request.Id);
        if (entity == null)
        {
            throw new KeyNotFoundException($"PatronCategory {request.Id} not found.");
        }
        await ValidateSeatTypeAsync(entity.TheaterId, request.SeatTypeId);
        await EnsureNotDuplicateAsync(entity.TheaterId, request.Name, request.SeatTypeId, excludeId: entity.Id);

        entity.Name        = request.Name;
        entity.Description = request.Description;
        entity.SeatTypeId  = request.SeatTypeId;
        entity.Price       = request.Price;
        entity.IsActive    = request.IsActive;
        await _uow.PatronCategoryStore.UpdateAsync(entity);

        var dto = entity.ToDTO<PatronCategory, PatronCategoryDTO>();
        await AttachSeatTypeInfoAsync(new[] { dto });
        return dto;
    }

    public async Task DeleteAsync(Guid id)
    {
        var entity = await _uow.PatronCategoryStore.GetByIdAsync(id);
        if (entity == null)
        {
            throw new KeyNotFoundException($"PatronCategory {id} not found.");
        }
        await _uow.PatronCategoryStore.DeleteAsync(entity);
    }

    private async Task ValidateSeatTypeAsync(Guid theaterId, Guid seatTypeId)
    {
        var exists = await _uow.SeatTypeStore.ExistsAsync(st => st.Id == seatTypeId && st.TheaterId == theaterId);
        if (!exists)
        {
            throw new InvalidOperationException("The seat kind does not belong to this theater.");
        }
    }

    private async Task EnsureNotDuplicateAsync(Guid theaterId, string name, Guid seatTypeId, Guid? excludeId)
    {
        var duplicate = await _uow.PatronCategoryStore.ExistsAsync(c =>
            c.TheaterId == theaterId && c.Name == name && c.SeatTypeId == seatTypeId &&
            (excludeId == null || c.Id != excludeId));
        if (duplicate)
        {
            throw new InvalidOperationException("A category with this name already exists for this seat kind.");
        }
    }

    private async Task AttachSeatTypeInfoAsync(IEnumerable<PatronCategoryDTO> items)
    {
        var itemList = items.ToList();
        var seatTypeIds = itemList.Select(x => x.SeatTypeId).Distinct().ToList();
        if (seatTypeIds.Count == 0)
        {
            return;
        }

        var seatTypes = (await _uow.SeatTypeStore.FindAsync(s => seatTypeIds.Contains(s.Id)))
            .ToDictionary(s => s.Id);

        foreach (var item in itemList)
        {
            if (seatTypes.TryGetValue(item.SeatTypeId, out var seatType))
            {
                item.SeatTypeName = seatType.Name;
                item.Kind         = seatType.Kind;
            }
        }
    }
}
