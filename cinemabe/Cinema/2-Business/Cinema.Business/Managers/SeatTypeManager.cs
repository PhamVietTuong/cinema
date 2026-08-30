using Cinema.Business.Contracts;
using Cinema.Business.DTO.Catalog;
using Cinema.Business.DTO.Requests;
using Cinema.Business.Extensions;
using Cinema.Business.Helpers;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;

namespace Cinema.Business.Managers;

public class SeatTypeManager : ISeatTypeManager
{
    protected readonly IApplicationUnitOfWork _uow;

    public SeatTypeManager(IApplicationUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await _uow.SeatTypeStore.ExistsAsync(e => e.Id == id);
    }

    private IQueryable<SeatType> GetFilteredSeatTypeQuery(Dictionary<string, string>? filters)
    {
        var query = _uow.SeatTypeStore.GetQuery();
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
                    query = _uow.SeatTypeStore.FilterQuery(query, e => e.Name.Contains(keyword));
                    break;

                case "theaterId":
                    if (Guid.TryParse(filters[key], out var theaterId))
                    {
                        query = _uow.SeatTypeStore.FilterQuery(query, e => e.TheaterId == theaterId);
                    }
                    break;
            }
        }
        return query;
    }

    public async Task<DefaultSearchResults<SeatTypeDTO>> GetAsync(PagingSearchDTO search)
    {
        search ??= new PagingSearchDTO();
        var (page, pageSize) = PagingHelper.ResolvePaging(search);

        var query = GetFilteredSeatTypeQuery(search.Filters);
        var total = await _uow.SeatTypeStore.CountAsync(query);
        var items = await _uow.SeatTypeStore.AllPageAsync(query, page - 1, pageSize);
        return PagingHelper.ToPagedResult<SeatType, SeatTypeDTO>(items, total, page, pageSize);
    }

    public async Task<SeatTypeDTO> GetByIdAsync(Guid id)
    {
        var entity = await _uow.SeatTypeStore.GetByIdAsync(id);
        if (entity == null)
        {
            throw new KeyNotFoundException($"SeatType {id} not found.");
        }
        return entity.ToDTO<SeatType, SeatTypeDTO>();
    }

    public async Task<SeatTypeDTO> CreateAsync(CreateSeatTypeRequest request)
    {
        var entity = request.ToNewEntity<CreateSeatTypeRequest, SeatType>();
        await _uow.SeatTypeStore.CreateAsync(entity);
        return entity.ToDTO<SeatType, SeatTypeDTO>();
    }

    public async Task<SeatTypeDTO> UpdateAsync(UpdateSeatTypeRequest request)
    {
        var entity = await _uow.SeatTypeStore.GetByIdAsync(request.Id);
        if (entity == null)
        {
            throw new KeyNotFoundException($"SeatType {request.Id} not found.");
        }
        entity.PatchEntity<SeatType, UpdateSeatTypeRequest>(request);
        await _uow.SeatTypeStore.UpdateAsync(entity);
        return entity.ToDTO<SeatType, SeatTypeDTO>();
    }

    public async Task DeleteAsync(Guid id)
    {
        await _uow.SeatTypeStore.DeleteAsync(id);
    }
}
