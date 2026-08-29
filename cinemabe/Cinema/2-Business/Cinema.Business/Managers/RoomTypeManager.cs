using System.Linq.Expressions;
using Cinema.Business.Contracts;
using Cinema.Business.DTO.Catalog;
using Cinema.Business.DTO.Requests;
using Cinema.Business.Extensions;
using Cinema.Business.Helpers;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;

namespace Cinema.Business.Managers;

public class RoomTypeManager(IApplicationUnitOfWork uow)
    : IRoomTypeManager
{
    protected readonly IApplicationUnitOfWork _uow = uow;

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await _uow.RoomTypeStore.ExistsAsync(e => e.Id == id);
    }

    public async Task<DefaultSearchResults<RoomTypeDTO>> GetAsync(PagingSearchDTO search)
    {
        search ??= new PagingSearchDTO();
        var (page, pageSize) = PagingHelper.ResolvePaging(search);
        var keyword = search.Filters.GetString("keyword");
        var theaterId = search.Filters.GetGuid("theaterId");

        Expression<Func<RoomType, bool>> predicate = e =>
            (string.IsNullOrEmpty(keyword) || e.Name.Contains(keyword!)) &&
            (theaterId == null || e.TheaterId == theaterId);

        var total = await _uow.RoomTypeStore.CountAsync(predicate);
        var items = await _uow.RoomTypeStore.FindAllPageAsync(page - 1, pageSize, predicate);
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
        var entity = request.ToNewEntity<CreateRoomTypeRequest, RoomType>();
        await _uow.RoomTypeStore.CreateAsync(entity);
        return entity.ToDTO<RoomType, RoomTypeDTO>();
    }

    public async Task<RoomTypeDTO> UpdateAsync(UpdateRoomTypeRequest request)
    {
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
        await _uow.RoomTypeStore.DeleteAsync(id);
    }
}
