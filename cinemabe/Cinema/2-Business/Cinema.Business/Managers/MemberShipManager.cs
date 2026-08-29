using System.Linq.Expressions;
using Cinema.Business.Contracts;
using Cinema.Business.DTO.Catalog;
using Cinema.Business.DTO.Requests;
using Cinema.Business.Extensions;
using Cinema.Business.Helpers;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;

namespace Cinema.Business.Managers;

public class MemberShipManager(IApplicationUnitOfWork uow)
    : IMemberShipManager
{
    protected readonly IApplicationUnitOfWork _uow = uow;

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await _uow.MemberShipStore.ExistsAsync(e => e.Id == id);
    }

    public async Task<DefaultSearchResults<MemberShipDTO>> GetAsync(PagingSearchDTO search)
    {
        search ??= new PagingSearchDTO();
        var (page, pageSize) = PagingHelper.ResolvePaging(search);
        var keyword = search.Filters.GetString("keyword");

        Expression<Func<MemberShip, bool>> predicate = e =>
            string.IsNullOrEmpty(keyword) || e.Name.Contains(keyword!);

        var total = await _uow.MemberShipStore.CountAsync(predicate);
        var items = await _uow.MemberShipStore.FindAllPageAsync(page - 1, pageSize, predicate);
        return PagingHelper.ToPagedResult<MemberShip, MemberShipDTO>(items, total, page, pageSize);
    }

    public async Task<MemberShipDTO> GetByIdAsync(Guid id)
    {
        var entity = await _uow.MemberShipStore.GetByIdAsync(id);
        if (entity == null)
        {
            throw new KeyNotFoundException($"MemberShip {id} not found.");
        }
        return entity.ToDTO<MemberShip, MemberShipDTO>();
    }

    public async Task<MemberShipDTO> CreateAsync(CreateMemberShipRequest request)
    {
        var entity = request.ToNewEntity<CreateMemberShipRequest, MemberShip>();
        await _uow.MemberShipStore.CreateAsync(entity);
        return entity.ToDTO<MemberShip, MemberShipDTO>();
    }

    public async Task<MemberShipDTO> UpdateAsync(UpdateMemberShipRequest request)
    {
        var entity = await _uow.MemberShipStore.GetByIdAsync(request.Id);
        if (entity == null)
        {
            throw new KeyNotFoundException($"MemberShip {request.Id} not found.");
        }
        entity.PatchEntity<MemberShip, UpdateMemberShipRequest>(request);
        await _uow.MemberShipStore.UpdateAsync(entity);
        return entity.ToDTO<MemberShip, MemberShipDTO>();
    }

    public async Task DeleteAsync(Guid id)
    {
        await _uow.MemberShipStore.DeleteAsync(id);
    }
}
