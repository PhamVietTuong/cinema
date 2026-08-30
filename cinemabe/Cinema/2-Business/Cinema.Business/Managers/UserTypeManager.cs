using Cinema.Business.Contracts;
using Cinema.Business.DTO.Catalog;
using Cinema.Business.DTO.Requests;
using Cinema.Business.Extensions;
using Cinema.Business.Helpers;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;

namespace Cinema.Business.Managers;

public class UserTypeManager : IUserTypeManager
{
    protected readonly IApplicationUnitOfWork _uow;

    public UserTypeManager(IApplicationUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await _uow.UserTypeStore.ExistsAsync(e => e.Id == id);
    }

    private IQueryable<UserType> GetFilteredUserTypeQuery(Dictionary<string, string>? filters)
    {
        var query = _uow.UserTypeStore.GetQuery();
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
                    query = _uow.UserTypeStore.FilterQuery(query, e => e.Name.Contains(keyword));
                    break;
            }
        }
        return query;
    }

    public async Task<DefaultSearchResults<UserTypeDTO>> GetAsync(PagingSearchDTO search)
    {
        search ??= new PagingSearchDTO();
        var (page, pageSize) = PagingHelper.ResolvePaging(search);

        var query = GetFilteredUserTypeQuery(search.Filters);
        var total = await _uow.UserTypeStore.CountAsync(query);
        var items = await _uow.UserTypeStore.AllPageAsync(query, page - 1, pageSize);
        return PagingHelper.ToPagedResult<UserType, UserTypeDTO>(items, total, page, pageSize);
    }

    public async Task<UserTypeDTO> GetByIdAsync(Guid id)
    {
        var entity = await _uow.UserTypeStore.GetByIdAsync(id);
        if (entity == null)
        {
            throw new KeyNotFoundException($"UserType {id} not found.");
        }
        return entity.ToDTO<UserType, UserTypeDTO>();
    }

    public async Task<UserTypeDTO> CreateAsync(CreateUserTypeRequest request)
    {
        var entity = request.ToNewEntity<CreateUserTypeRequest, UserType>();
        await _uow.UserTypeStore.CreateAsync(entity);
        return entity.ToDTO<UserType, UserTypeDTO>();
    }

    public async Task<UserTypeDTO> UpdateAsync(UpdateUserTypeRequest request)
    {
        var entity = await _uow.UserTypeStore.GetByIdAsync(request.Id);
        if (entity == null)
        {
            throw new KeyNotFoundException($"UserType {request.Id} not found.");
        }
        entity.PatchEntity<UserType, UpdateUserTypeRequest>(request);
        await _uow.UserTypeStore.UpdateAsync(entity);
        return entity.ToDTO<UserType, UserTypeDTO>();
    }

    public async Task DeleteAsync(Guid id)
    {
        await _uow.UserTypeStore.DeleteAsync(id);
    }
}
