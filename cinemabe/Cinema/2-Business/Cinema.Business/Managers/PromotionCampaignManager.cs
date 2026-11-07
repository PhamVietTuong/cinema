using Cinema.Business.Contracts;
using Cinema.Business.DTO.Catalog;
using Cinema.Business.DTO.Requests;
using Cinema.Business.Extensions;
using Cinema.Business.Helpers;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;

namespace Cinema.Business.Managers;

public class PromotionCampaignManager : IPromotionCampaignManager
{
    protected readonly IApplicationUnitOfWork _uow;

    public PromotionCampaignManager(IApplicationUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await _uow.PromotionCampaignStore.ExistsAsync(e => e.Id == id);
    }

    private IQueryable<PromotionCampaign> GetFilteredQuery(Dictionary<string, string>? filters)
    {
        var query = _uow.PromotionCampaignStore.GetQuery();
        var keyword = filters.GetString("keyword");
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = _uow.PromotionCampaignStore.FilterQuery(query, e => e.Name.Contains(keyword));
        }
        return query;
    }

    public async Task<DefaultSearchResults<PromotionCampaignDTO>> GetAsync(PagingSearchDTO search)
    {
        search ??= new PagingSearchDTO();
        var (page, pageSize) = PagingHelper.ResolvePaging(search);
        var query = GetFilteredQuery(search.Filters).OrderBy(c => c.Name);
        var total = await _uow.PromotionCampaignStore.CountAsync(query);
        var items = await _uow.PromotionCampaignStore.AllPageAsync(query, page - 1, pageSize);
        return PagingHelper.ToPagedResult<PromotionCampaign, PromotionCampaignDTO>(items, total, page, pageSize);
    }

    public async Task<PromotionCampaignDTO> GetByIdAsync(Guid id)
    {
        var entity = await _uow.PromotionCampaignStore.GetByIdAsync(id);
        if (entity == null)
        {
            throw new KeyNotFoundException($"PromotionCampaign {id} not found.");
        }
        return entity.ToDTO<PromotionCampaign, PromotionCampaignDTO>();
    }

    public async Task<PromotionCampaignDTO> CreateAsync(CreatePromotionCampaignRequest request)
    {
        await ValidateNameAsync(request.Name, null);
        var entity = request.ToNewEntity<CreatePromotionCampaignRequest, PromotionCampaign>();
        entity.Name = entity.Name.Trim();
        await _uow.PromotionCampaignStore.CreateAsync(entity);
        return entity.ToDTO<PromotionCampaign, PromotionCampaignDTO>();
    }

    public async Task<PromotionCampaignDTO> UpdateAsync(UpdatePromotionCampaignRequest request)
    {
        var entity = await _uow.PromotionCampaignStore.GetByIdAsync(request.Id);
        if (entity == null)
        {
            throw new KeyNotFoundException($"PromotionCampaign {request.Id} not found.");
        }
        await ValidateNameAsync(request.Name, request.Id);
        entity.PatchEntity<PromotionCampaign, UpdatePromotionCampaignRequest>(request);
        entity.Name = entity.Name.Trim();
        await _uow.PromotionCampaignStore.UpdateAsync(entity);
        return entity.ToDTO<PromotionCampaign, PromotionCampaignDTO>();
    }

    public async Task DeleteAsync(Guid id)
    {
        await _uow.PromotionCampaignStore.DeleteAsync(id);
    }

    private async Task ValidateNameAsync(string name, Guid? selfId)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("Campaign name is required.");
        }
        var trimmed = name.Trim();
        var duplicate = await _uow.PromotionCampaignStore.ExistsAsync(c => c.Name == trimmed && c.Id != (selfId ?? Guid.Empty));
        if (duplicate)
        {
            throw new InvalidOperationException($"A campaign named \"{trimmed}\" already exists.");
        }
    }
}
