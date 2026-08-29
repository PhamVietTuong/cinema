using System.Linq.Expressions;
using Cinema.Business.Contracts;
using Cinema.Business.DTO.Catalog;
using Cinema.Business.DTO.Requests;
using Cinema.Business.Extensions;
using Cinema.Business.Helpers;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;

namespace Cinema.Business.Managers;

public class NewsManager(IApplicationUnitOfWork uow)
    : INewsManager
{
    protected readonly IApplicationUnitOfWork _uow = uow;

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await _uow.NewsStore.ExistsAsync(e => e.Id == id);
    }

    public async Task<DefaultSearchResults<NewsDTO>> GetAsync(PagingSearchDTO search)
    {
        search ??= new PagingSearchDTO();
        var (page, pageSize) = PagingHelper.ResolvePaging(search);
        var keyword = search.Filters.GetString("keyword");
        var isPublished = search.Filters.GetBool("isPublished");

        Expression<Func<News, bool>> predicate = e =>
            (string.IsNullOrEmpty(keyword) || e.Title.Contains(keyword!)) &&
            (isPublished == null || e.IsPublished == isPublished);

        var total = await _uow.NewsStore.CountAsync(predicate);
        var items = await _uow.NewsStore.FindAllPageAsync(page - 1, pageSize, predicate);
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
