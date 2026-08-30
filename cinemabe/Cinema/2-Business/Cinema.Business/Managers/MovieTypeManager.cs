using Cinema.Business.Contracts;
using Cinema.Business.DTO.Catalog;
using Cinema.Business.DTO.Requests;
using Cinema.Business.Extensions;
using Cinema.Business.Helpers;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;

namespace Cinema.Business.Managers;

public class MovieTypeManager : IMovieTypeManager
{
    protected readonly IApplicationUnitOfWork _uow;

    public MovieTypeManager(IApplicationUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await _uow.MovieTypeStore.ExistsAsync(e => e.Id == id);
    }

    private IQueryable<MovieType> GetFilteredMovieTypeQuery(Dictionary<string, string>? filters)
    {
        var query = _uow.MovieTypeStore.GetQuery();
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
                    query = _uow.MovieTypeStore.FilterQuery(query, e => e.Name.Contains(keyword));
                    break;
            }
        }
        return query;
    }

    public async Task<DefaultSearchResults<MovieTypeDTO>> GetAsync(PagingSearchDTO search)
    {
        search ??= new PagingSearchDTO();
        var (page, pageSize) = PagingHelper.ResolvePaging(search);

        var query = GetFilteredMovieTypeQuery(search.Filters);
        var total = await _uow.MovieTypeStore.CountAsync(query);
        var items = await _uow.MovieTypeStore.AllPageAsync(query, page - 1, pageSize);
        return PagingHelper.ToPagedResult<MovieType, MovieTypeDTO>(items, total, page, pageSize);
    }

    public async Task<MovieTypeDTO> GetByIdAsync(Guid id)
    {
        var entity = await _uow.MovieTypeStore.GetByIdAsync(id);
        if (entity == null)
        {
            throw new KeyNotFoundException($"MovieType {id} not found.");
        }
        return entity.ToDTO<MovieType, MovieTypeDTO>();
    }

    public async Task<MovieTypeDTO> CreateAsync(CreateMovieTypeRequest request)
    {
        var entity = request.ToNewEntity<CreateMovieTypeRequest, MovieType>();
        await _uow.MovieTypeStore.CreateAsync(entity);
        return entity.ToDTO<MovieType, MovieTypeDTO>();
    }

    public async Task<MovieTypeDTO> UpdateAsync(UpdateMovieTypeRequest request)
    {
        var entity = await _uow.MovieTypeStore.GetByIdAsync(request.Id);
        if (entity == null)
        {
            throw new KeyNotFoundException($"MovieType {request.Id} not found.");
        }
        entity.PatchEntity<MovieType, UpdateMovieTypeRequest>(request);
        await _uow.MovieTypeStore.UpdateAsync(entity);
        return entity.ToDTO<MovieType, MovieTypeDTO>();
    }

    public async Task DeleteAsync(Guid id)
    {
        await _uow.MovieTypeStore.DeleteAsync(id);
    }
}
