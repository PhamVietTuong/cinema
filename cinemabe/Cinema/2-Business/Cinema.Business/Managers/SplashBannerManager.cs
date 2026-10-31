using Cinema.Business.Contracts;
using Cinema.Business.DTO.Catalog;
using Cinema.Business.DTO.Requests;
using Cinema.Business.Extensions;
using Cinema.Business.Helpers;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;

namespace Cinema.Business.Managers;

public class SplashBannerManager : ISplashBannerManager
{
    private readonly IApplicationUnitOfWork _uow;
    private readonly TimeProvider _clock;

    public SplashBannerManager(IApplicationUnitOfWork uow, TimeProvider clock)
    {
        _uow = uow;
        _clock = clock;
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await _uow.SplashBannerStore.ExistsAsync(e => e.Id == id);
    }

    private IQueryable<SplashBanner> GetFilteredQuery(Dictionary<string, string>? filters)
    {
        var query = _uow.SplashBannerStore.GetQuery();
        var keyword = filters.GetString("keyword");
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = _uow.SplashBannerStore.FilterQuery(query, e => e.Title.Contains(keyword));
        }
        var isPublished = filters.GetBool("isPublished");
        if (isPublished.HasValue)
        {
            query = _uow.SplashBannerStore.FilterQuery(query, e => e.IsPublished == isPublished.Value);
        }
        return query;
    }

    public async Task<DefaultSearchResults<SplashBannerDTO>> GetAsync(PagingSearchDTO search)
    {
        search ??= new PagingSearchDTO();
        var (page, pageSize) = PagingHelper.ResolvePaging(search);

        var query = GetFilteredQuery(search.Filters).OrderByDescending(e => e.StartDate).ThenByDescending(e => e.CreationTime);
        var total = await _uow.SplashBannerStore.CountAsync(query);
        var items = await _uow.SplashBannerStore.AllPageAsync(query, page - 1, pageSize);
        return PagingHelper.ToPagedResult<SplashBanner, SplashBannerDTO>(items, total, page, pageSize);
    }

    public async Task<List<SplashBannerDTO>> GetActiveAsync()
    {
        var today = BusinessCalendar.LocalNow(_clock).Date;
        var active = await _uow.SplashBannerStore.GetActiveAsync(today);
        if (active == null)
        {
            return new List<SplashBannerDTO>();
        }
        return new List<SplashBannerDTO> { active.ToDTO<SplashBanner, SplashBannerDTO>() };
    }

    public async Task<SplashBannerDTO> GetByIdAsync(Guid id)
    {
        var entity = await _uow.SplashBannerStore.GetByIdAsync(id);
        if (entity == null)
        {
            throw new KeyNotFoundException($"SplashBanner {id} not found.");
        }
        return entity.ToDTO<SplashBanner, SplashBannerDTO>();
    }

    public async Task<SplashBannerDTO> CreateAsync(CreateSplashBannerRequest request)
    {
        var linkUrl = Normalize(request.LinkUrl);
        Validate(request.Title, request.ImageUrl, linkUrl, request.StartDate, request.EndDate);

        var entity = new SplashBanner
        {
            Title = request.Title.Trim(),
            ImageUrl = request.ImageUrl.Trim(),
            LinkUrl = linkUrl,
            StartDate = request.StartDate.Date,
            EndDate = request.EndDate.Date,
            IsPublished = request.IsPublished,
        };
        await _uow.SplashBannerStore.CreateAsync(entity);
        return entity.ToDTO<SplashBanner, SplashBannerDTO>();
    }

    public async Task<SplashBannerDTO> UpdateAsync(UpdateSplashBannerRequest request)
    {
        var entity = await _uow.SplashBannerStore.GetByIdAsync(request.Id);
        if (entity == null)
        {
            throw new KeyNotFoundException($"SplashBanner {request.Id} not found.");
        }

        var linkUrl = Normalize(request.LinkUrl);
        Validate(request.Title, request.ImageUrl, linkUrl, request.StartDate, request.EndDate);

        entity.Title = request.Title.Trim();
        entity.ImageUrl = request.ImageUrl.Trim();
        entity.LinkUrl = linkUrl;
        entity.StartDate = request.StartDate.Date;
        entity.EndDate = request.EndDate.Date;
        entity.IsPublished = request.IsPublished;
        entity.LastUpdatedTime = DateTime.UtcNow;

        await _uow.SaveChangesAsync();
        return entity.ToDTO<SplashBanner, SplashBannerDTO>();
    }

    public async Task DeleteAsync(Guid id)
    {
        await _uow.SplashBannerStore.DeleteAsync(id);
    }

    // ── Validation ───────────────────────────────────────────────────────────

    private static void Validate(string title, string imageUrl, string? linkUrl, DateTime startDate, DateTime endDate)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new InvalidOperationException("Title is required.");
        }
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            throw new InvalidOperationException("An image is required.");
        }
        if (endDate.Date < startDate.Date)
        {
            throw new InvalidOperationException("End date must not be before start date.");
        }
        if (linkUrl != null && !IsAllowedLink(linkUrl))
        {
            throw new InvalidOperationException("Link must be an http(s) URL or a site path starting with '/'.");
        }
    }

    private static bool IsAllowedLink(string linkUrl)
    {
        if (linkUrl.StartsWith('/') && !linkUrl.StartsWith("//") && !linkUrl.StartsWith(@"/\"))
        {
            return true;
        }
        return Uri.TryCreate(linkUrl, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    private static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }
        return value.Trim();
    }
}
