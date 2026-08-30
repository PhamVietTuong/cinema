using Cinema.Business.Contracts;
using Cinema.Business.DTO.Catalog;
using Cinema.Business.DTO.Requests;
using Cinema.Business.Extensions;
using Cinema.Business.Helpers;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;

namespace Cinema.Business.Managers;

public class NewsManager : INewsManager
{
    protected readonly IApplicationUnitOfWork _uow;

    public NewsManager(IApplicationUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await _uow.NewsStore.ExistsAsync(e => e.Id == id);
    }

    private IQueryable<News> GetFilteredNewsQuery(Dictionary<string, string>? filters)
    {
        var query = _uow.NewsStore.GetQuery();
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
                    query = _uow.NewsStore.FilterQuery(query, e => e.Title.Contains(keyword));
                    break;

                case "isPublished":
                    if (bool.TryParse(filters[key], out var isPublished))
                    {
                        query = _uow.NewsStore.FilterQuery(query, e => e.IsPublished == isPublished);
                    }
                    break;
            }
        }
        return query;
    }

    public async Task<DefaultSearchResults<NewsDTO>> GetAsync(PagingSearchDTO search)
    {
        search ??= new PagingSearchDTO();
        var (page, pageSize) = PagingHelper.ResolvePaging(search);

        var query = GetFilteredNewsQuery(search.Filters);
        var total = await _uow.NewsStore.CountAsync(query);
        var items = await _uow.NewsStore.AllPageAsync(query, page - 1, pageSize);
        return PagingHelper.ToPagedResult<News, NewsDTO>(items, total, page, pageSize);
    }

    public async Task<NewsDTO> GetByIdAsync(Guid id)
    {
        var entity = await _uow.NewsStore.GetByIdAsync(id);
        if (entity == null)
        {
            throw new KeyNotFoundException($"News {id} not found.");
        }
        return entity.ToDTO<News, NewsDTO>();
    }

    public async Task<NewsDTO> CreateAsync(CreateNewsRequest request)
    {
        var entity = request.ToNewEntity<CreateNewsRequest, News>();
        await _uow.NewsStore.CreateAsync(entity);
        return entity.ToDTO<News, NewsDTO>();
    }

    public async Task<NewsDTO> UpdateAsync(UpdateNewsRequest request)
    {
        var entity = await _uow.NewsStore.GetByIdAsync(request.Id);
        if (entity == null)
        {
            throw new KeyNotFoundException($"News {request.Id} not found.");
        }
        entity.PatchEntity<News, UpdateNewsRequest>(request);
        await _uow.NewsStore.UpdateAsync(entity);
        return entity.ToDTO<News, NewsDTO>();
    }

    public async Task DeleteAsync(Guid id)
    {
        await _uow.NewsStore.DeleteAsync(id);
    }
}
