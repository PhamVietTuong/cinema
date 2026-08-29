using System.Linq.Expressions;
using Cinema.Business.Contracts;
using Cinema.Business.DTO.Catalog;
using Cinema.Business.DTO.Requests;
using Cinema.Business.Extensions;
using Cinema.Business.Helpers;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;

namespace Cinema.Business.Managers;

public class HolidayManager(IApplicationUnitOfWork uow)
    : IHolidayManager
{
    protected readonly IApplicationUnitOfWork _uow = uow;

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await _uow.HolidayStore.ExistsAsync(e => e.Id == id);
    }

    public async Task<DefaultSearchResults<HolidayDTO>> GetAsync(PagingSearchDTO search)
    {
        search ??= new PagingSearchDTO();
        var (page, pageSize) = PagingHelper.ResolvePaging(search);
        var keyword = search.Filters.GetString("keyword");

        Expression<Func<Holiday, bool>> predicate = e =>
            string.IsNullOrEmpty(keyword) || e.Name.Contains(keyword!);

        var total = await _uow.HolidayStore.CountAsync(predicate);
        var items = await _uow.HolidayStore.FindAllPageAsync(page - 1, pageSize, predicate);
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
