using Cinema.Business.Contracts;
using Cinema.Business.DTO.Catalog;
using Cinema.Business.DTO.Requests;
using Cinema.Business.Extensions;
using Cinema.Business.Helpers;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;

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
                case "keyword":
                    var keyword = filters[key];
                    query = _uow.HolidayStore.FilterQuery(query, e => e.Name.Contains(keyword));
                    break;
            }
        }
        return query;
    }

    public async Task<DefaultSearchResults<HolidayDTO>> GetAsync(PagingSearchDTO search)
    {
        search ??= new PagingSearchDTO();
        var (page, pageSize) = PagingHelper.ResolvePaging(search);

        var query = GetFilteredHolidayQuery(search.Filters);
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
        await _uow.HolidayStore.DeleteAsync(id);
    }
}
