using System.Linq.Expressions;
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

    public async Task<DefaultSearchResults<SeatTypeDTO>> GetAsync(PagingSearchDTO search)
    {
        search ??= new PagingSearchDTO();
        var (page, pageSize) = PagingHelper.ResolvePaging(search);
        var keyword = search.Filters.GetString("keyword");
        var theaterId = search.Filters.GetGuid("theaterId");

        Expression<Func<SeatType, bool>> predicate = e =>
            (string.IsNullOrEmpty(keyword) || e.Name.Contains(keyword!)) &&
            (theaterId == null || e.TheaterId == theaterId);

        var total = await _uow.SeatTypeStore.CountAsync(predicate);
        var items = await _uow.SeatTypeStore.FindAllPageAsync(page - 1, pageSize, predicate);
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
