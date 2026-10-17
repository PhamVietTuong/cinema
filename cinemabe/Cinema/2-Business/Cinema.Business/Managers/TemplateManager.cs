using Cinema.Business.Contracts;
using Cinema.Business.DTO.Catalog;
using Cinema.Business.DTO.Requests;
using Cinema.Business.Extensions;
using Cinema.Business.Helpers;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Cinema.Data.Enums;

namespace Cinema.Business.Managers;

public class TemplateManager : ITemplateManager
{
    protected readonly IApplicationUnitOfWork _uow;

    public TemplateManager(IApplicationUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await _uow.TemplateStore.ExistsAsync(e => e.Id == id);
    }

    private IQueryable<Template> GetFilteredTemplateQuery(Dictionary<string, string>? filters)
    {
        var query = _uow.TemplateStore.GetQuery();
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
                    query = _uow.TemplateStore.FilterQuery(query, e => e.Code.Contains(keyword) || e.Name.Contains(keyword));
                    break;

                case "name":
                    var name = filters[key];
                    query = _uow.TemplateStore.FilterQuery(query, e => e.Name.Contains(name));
                    break;

                case "channel":
                    if (int.TryParse(filters[key], out var channelValue))
                    {
                        var channel = (TemplateChannel)channelValue;
                        query = _uow.TemplateStore.FilterQuery(query, e => e.Channel == channel);
                    }
                    break;
            }
        }
        return query;
    }

    private IQueryable<Template> ApplySort(IQueryable<Template> query, SortDTO? sort)
    {
        if (sort == null || string.IsNullOrEmpty(sort.Field))
        {
            return _uow.TemplateStore.OrderQuery(query, e => e.Code, true);
        }

        return sort.Field switch
        {
            "code" => _uow.TemplateStore.OrderQuery(query, e => e.Code, sort.Ascending),
            "name" => _uow.TemplateStore.OrderQuery(query, e => e.Name, sort.Ascending),
            "channel" => _uow.TemplateStore.OrderQuery(query, e => e.Channel, sort.Ascending),
            _ => _uow.TemplateStore.OrderQuery(query, e => e.Code, true),
        };
    }

    public async Task<DefaultSearchResults<TemplateDTO>> GetAsync(PagingSearchDTO search)
    {
        search ??= new PagingSearchDTO();
        var (page, pageSize) = PagingHelper.ResolvePaging(search);

        var query = GetFilteredTemplateQuery(search.Filters);
        query = ApplySort(query, search.Sort);
        var total = await _uow.TemplateStore.CountAsync(query);
        var items = await _uow.TemplateStore.AllPageAsync(query, page - 1, pageSize);
        return PagingHelper.ToPagedResult<Template, TemplateDTO>(items, total, page, pageSize);
    }

    public async Task<TemplateDTO> GetByIdAsync(Guid id)
    {
        var entity = await _uow.TemplateStore.GetByIdAsync(id);
        if (entity == null)
        {
            throw new KeyNotFoundException($"Template {id} not found.");
        }
        return entity.ToDTO<Template, TemplateDTO>();
    }

    public async Task<TemplateDTO> CreateAsync(CreateTemplateRequest request)
    {
        var code = request.Code.Trim();
        if (await _uow.TemplateStore.ExistsAsync(e => e.Code == code))
        {
            throw new InvalidOperationException("A template with this code already exists.");
        }
        if (request.Channel == TemplateChannel.Email && string.IsNullOrWhiteSpace(request.Subject))
        {
            throw new InvalidOperationException("Subject is required for email templates.");
        }

        var entity = request.ToNewEntity<CreateTemplateRequest, Template>();
        entity.Code = code;
        await _uow.TemplateStore.CreateAsync(entity);
        return entity.ToDTO<Template, TemplateDTO>();
    }

    public async Task<TemplateDTO> UpdateAsync(UpdateTemplateRequest request)
    {
        var entity = await _uow.TemplateStore.GetByIdAsync(request.Id);
        if (entity == null)
        {
            throw new KeyNotFoundException($"Template {request.Id} not found.");
        }
        if (request.Channel == TemplateChannel.Email && string.IsNullOrWhiteSpace(request.Subject))
        {
            throw new InvalidOperationException("Subject is required for email templates.");
        }
        entity.PatchEntity<Template, UpdateTemplateRequest>(request);
        await _uow.TemplateStore.UpdateAsync(entity);
        return entity.ToDTO<Template, TemplateDTO>();
    }

    public async Task DeleteAsync(Guid id)
    {
        await _uow.TemplateStore.DeleteAsync(id);
    }
}
