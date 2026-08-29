using System.Linq.Expressions;
using Cinema.Business.Contracts;
using Cinema.Business.DTO.Catalog;
using Cinema.Business.DTO.Requests;
using Cinema.Business.Extensions;
using Cinema.Business.Helpers;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;

namespace Cinema.Business.Managers;

public class UserTypeManager(IApplicationUnitOfWork uow)
    : IUserTypeManager
{
    protected readonly IApplicationUnitOfWork _uow = uow;

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await _uow.UserTypeStore.ExistsAsync(e => e.Id == id);
    }

    public async Task<DefaultSearchResults<UserTypeDTO>> GetAsync(PagingSearchDTO search)
    {
        search ??= new PagingSearchDTO();
        var (page, pageSize) = PagingHelper.ResolvePaging(search);
        var keyword = search.Filters.GetString("keyword");

        Expression<Func<UserType, bool>> predicate = e =>
            string.IsNullOrEmpty(keyword) || e.Name.Contains(keyword!);

        var total = await _uow.UserTypeStore.CountAsync(predicate);
        var items = await _uow.UserTypeStore.FindAllPageAsync(page - 1, pageSize, predicate);
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
