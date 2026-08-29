using Cinema.Business.Contracts;
using Cinema.Business.DTO.Catalog;
using Cinema.Business.DTO.Requests;
using Cinema.Business.Extensions;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;

namespace Cinema.Business.Managers;

public class DiscountManager : IDiscountManager
{
    protected readonly IApplicationUnitOfWork _uow;

    public DiscountManager(IApplicationUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await _uow.DiscountStore.ExistsAsync(e => e.Id == id);
    }

    private static bool Match(Discount e, string kw)
    {
        return (e.Code ?? string.Empty).Contains(kw, StringComparison.OrdinalIgnoreCase)
            || (e.Description ?? string.Empty).Contains(kw, StringComparison.OrdinalIgnoreCase);
    }

    // Overridden so the promotion's theater scope (join rows) is loaded and projected.
    public async Task<DefaultSearchResults<DiscountDTO>> GetAsync(PagingSearchDTO search)
    {
        search ??= new PagingSearchDTO();
        var all = await _uow.DiscountStore.GetAllWithScopeAsync();

        var keyword = search.Filters.GetString("keyword");
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            all = all.Where(e => Match(e, keyword)).ToList();
        }

        var page = search.PageIndex > 0 ? search.PageIndex : 1;
        var pageSize = search.PageSize > 0 ? search.PageSize : 20;
        var total = all.Count;
        var paged = all.Skip((page - 1) * pageSize).Take(pageSize).Select(ToDiscountDTO).ToList();

        return new DefaultSearchResults<DiscountDTO>
        {
            Results = paged, TotalCount = total, CountPerPage = pageSize, Page = page
        };
    }

    public async Task<DiscountDTO> GetByIdAsync(Guid id)
    {
        var entity = await _uow.DiscountStore.GetByIdWithScopeAsync(id);
        if (entity == null)
        {
            throw new KeyNotFoundException($"Discount {id} not found.");
        }
        return ToDiscountDTO(entity);
    }

    public async Task<DiscountDTO> CreateAsync(CreateDiscountRequest request)
    {
        var entity = request.ToNewEntity<CreateDiscountRequest, Discount>();
        entity.Code = Normalize(request.Code);
        entity.StartTimeOfDay = ParseTime(request.StartTimeOfDay);
        entity.EndTimeOfDay = ParseTime(request.EndTimeOfDay);
        ApplyTheaterScope(entity, request.ApplyToAllTheaters, request.TheaterIds);
        await _uow.DiscountStore.CreateAsync(entity);
        return ToDiscountDTO(entity);
    }

    public async Task<DiscountDTO> UpdateAsync(UpdateDiscountRequest request)
    {
        var entity = await _uow.DiscountStore.GetByIdWithScopeAsync(request.Id);
        if (entity == null)
        {
            throw new KeyNotFoundException($"Discount {request.Id} not found.");
        }

        entity.PatchEntity<Discount, UpdateDiscountRequest>(request);
        // Explicitly assign nullable/renamed fields the reflection patch skips when they're null.
        entity.Code = Normalize(request.Code);
        entity.Description = request.Description;
        entity.MaxDiscountAmount = request.MaxDiscountAmount;
        entity.MaxUsage = request.MaxUsage;
        entity.MovieId = request.MovieId;
        entity.DaysOfWeekMask = request.DaysOfWeekMask;
        entity.StartTimeOfDay = ParseTime(request.StartTimeOfDay);
        entity.EndTimeOfDay = ParseTime(request.EndTimeOfDay);
        entity.LastUpdatedTime = DateTime.UtcNow;
        ApplyTheaterScope(entity, request.ApplyToAllTheaters, request.TheaterIds);

        // entity is tracked (loaded with scope) — one SaveChanges persists the patch and join add/removes.
        await _uow.SaveChangesAsync();
        return ToDiscountDTO(entity);
    }

    public async Task DeleteAsync(Guid id)
    {
        await _uow.DiscountStore.DeleteAsync(id);
    }

    /// <summary>Reconciles the promotion's theater join rows to the requested set.</summary>
    private static void ApplyTheaterScope(Discount entity, bool applyToAll, List<Guid>? theaterIds)
    {
        entity.ApplyToAllTheaters = applyToAll;
        var wanted = applyToAll || theaterIds == null
            ? new HashSet<Guid>()
            : theaterIds.Where(id => id != Guid.Empty).ToHashSet();

        foreach (var link in entity.DiscountTheaters.Where(t => !wanted.Contains(t.TheaterId)).ToList())
        {
            entity.DiscountTheaters.Remove(link);
        }

        var existing = entity.DiscountTheaters.Select(t => t.TheaterId).ToHashSet();
        foreach (var id in wanted.Where(id => !existing.Contains(id)))
        {
            entity.DiscountTheaters.Add(new DiscountTheater { TheaterId = id });
        }
    }

    private static string? Normalize(string? code)
        => string.IsNullOrWhiteSpace(code) ? null : code.Trim();

    private static TimeOnly? ParseTime(string? value)
        => TimeOnly.TryParse(value, out var t) ? t : null;

    private static DiscountDTO ToDiscountDTO(Discount d)
    {
        var dto = d.ToDTO<Discount, DiscountDTO>();
        dto.TheaterIds = d.DiscountTheaters?.Select(t => t.TheaterId).ToList() ?? new();
        dto.StartTimeOfDay = d.StartTimeOfDay?.ToString("HH:mm");
        dto.EndTimeOfDay = d.EndTimeOfDay?.ToString("HH:mm");
        return dto;
    }
}
