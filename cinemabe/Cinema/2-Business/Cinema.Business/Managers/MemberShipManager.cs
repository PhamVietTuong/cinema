using Cinema.Business.Contracts;
using Cinema.Business.DTO.Catalog;
using Cinema.Business.DTO.Requests;
using Cinema.Business.Extensions;
using Cinema.Business.Helpers;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;

namespace Cinema.Business.Managers;

public class MemberShipManager : IMemberShipManager
{
    protected readonly IApplicationUnitOfWork _uow;

    public MemberShipManager(IApplicationUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await _uow.MemberShipStore.ExistsAsync(e => e.Id == id);
    }

    private IQueryable<MemberShip> GetFilteredMemberShipQuery(Dictionary<string, string>? filters)
    {
        var query = _uow.MemberShipStore.GetQuery();
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
                    query = _uow.MemberShipStore.FilterQuery(query, e => e.Name.Contains(keyword));
                    break;
            }
        }
        return query;
    }

    public async Task<DefaultSearchResults<MemberShipDTO>> GetAsync(PagingSearchDTO search)
    {
        search ??= new PagingSearchDTO();
        var (page, pageSize) = PagingHelper.ResolvePaging(search);

        var query = GetFilteredMemberShipQuery(search.Filters);
        var total = await _uow.MemberShipStore.CountAsync(query);
        var items = await _uow.MemberShipStore.AllPageAsync(query, page - 1, pageSize);
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
