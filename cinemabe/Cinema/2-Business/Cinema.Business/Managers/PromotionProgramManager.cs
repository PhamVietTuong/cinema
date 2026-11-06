using Cinema.Business.Contracts;
using Cinema.Business.DTO.Catalog;
using Cinema.Business.DTO.Requests;
using Cinema.Business.Extensions;
using Cinema.Business.Helpers;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Cinema.Data.Enums;

namespace Cinema.Business.Managers;

public class PromotionProgramManager : IPromotionProgramManager
{
    protected readonly IApplicationUnitOfWork _uow;

    public PromotionProgramManager(IApplicationUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<bool> ExistsAsync(Guid id)
        => await _uow.PromotionProgramStore.ExistsAsync(e => e.Id == id);

    private IQueryable<PromotionProgram> GetFilteredQuery(Dictionary<string, string>? filters)
    {
        var query = _uow.PromotionProgramStore.GetQuery();
        var keyword = filters.GetString("keyword");
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = _uow.PromotionProgramStore.FilterQuery(query, e => e.Title.Contains(keyword));
        }
        var isPublished = filters.GetBool("isPublished");
        if (isPublished.HasValue)
        {
            query = _uow.PromotionProgramStore.FilterQuery(query, e => e.IsPublished == isPublished.Value);
        }
        return query;
    }

    public async Task<DefaultSearchResults<PromotionProgramDTO>> GetAsync(PagingSearchDTO search)
    {
        search ??= new PagingSearchDTO();
        var (page, pageSize) = PagingHelper.ResolvePaging(search);
        var query = GetFilteredQuery(search.Filters).OrderBy(p => p.DisplayOrder).ThenByDescending(p => p.CreationTime);

        var total = await _uow.PromotionProgramStore.CountAsync(query);
        var items = await _uow.PromotionProgramStore.AllPageAsync(query, page - 1, pageSize);
        var dtos = await MapPageAsync(items);
        return new DefaultSearchResults<PromotionProgramDTO> { Results = dtos, TotalCount = total, CountPerPage = pageSize, Page = page };
    }

    public async Task<DefaultSearchResults<PromotionProgramDTO>> GetPublishedAsync(PagingSearchDTO search)
    {
        search ??= new PagingSearchDTO();
        var (page, pageSize) = PagingHelper.ResolvePaging(search);
        var now = DateTime.UtcNow;
        var theaterId = search.Filters.GetGuid("theaterId");

        var query = _uow.PromotionProgramStore.GetQuery().Where(p => p.IsPublished && p.EndDate >= now);
        if (theaterId is Guid tid)
        {
            query = query.Where(p => !p.Schedules.Any() || p.Schedules.Any(s => s.TheaterId == null || s.TheaterId == tid));
        }
        query = query.OrderBy(p => p.DisplayOrder).ThenBy(p => p.StartDate);

        var total = await _uow.PromotionProgramStore.CountAsync(query);
        var items = await _uow.PromotionProgramStore.AllPageAsync(query, page - 1, pageSize);
        var dtos = await MapPageAsync(items);
        return new DefaultSearchResults<PromotionProgramDTO> { Results = dtos, TotalCount = total, CountPerPage = pageSize, Page = page };
    }

    public async Task<PromotionProgramDTO> GetByIdAsync(Guid id)
    {
        var entity = await _uow.PromotionProgramStore.GetByIdAsync(id);
        if (entity == null)
        {
            throw new KeyNotFoundException($"PromotionProgram {id} not found.");
        }
        var dtos = await MapPageAsync(new List<PromotionProgram> { entity });
        return dtos[0];
    }

    public async Task<PromotionProgramDTO> CreateAsync(CreatePromotionProgramRequest request)
    {
        await ValidateAsync(request.Mechanic, request.PercentOff, request.Eligibility, request.MinMemberShipId, request.MaxAge,
            request.StartDate, request.EndDate, request.Prices, request.Schedules, request.AutoApply, request.Code,
            request.MaxTicketsPerTransaction, request.MovieId, null);

        var entity = new PromotionProgram
        {
            Title = request.Title.Trim(),
            Summary = Normalize(request.Summary),
            BannerUrl = Normalize(request.BannerUrl),
            Category = request.Category,
            Mechanic = request.Mechanic,
            Eligibility = request.Eligibility,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            ExcludeHolidays = request.ExcludeHolidays,
            AllowedChannelsMask = request.AllowedChannelsMask,
            MaxTicketsPerTransaction = request.MaxTicketsPerTransaction,
            StacksWithMembershipDiscount = request.StacksWithMembershipDiscount,
            TermsText = Normalize(request.TermsText),
            DisplayOrder = request.DisplayOrder,
            IsPublished = request.IsPublished,
            AutoApply = request.AutoApply,
            Code = NormalizeCode(request.Code),
            MaxUsage = request.MaxUsage,
            MaxDiscountAmount = request.MaxDiscountAmount,
            MovieId = request.MovieId,
        };
        ApplyMechanicAndEligibility(entity, request.PercentOff, request.MinMemberShipId, request.MaxAge, request.Prices);
        ReplaceSchedules(entity, request.Schedules);

        await _uow.PromotionProgramStore.CreateAsync(entity);
        var dtos = await MapPageAsync(new List<PromotionProgram> { entity });
        return dtos[0];
    }

    public async Task<PromotionProgramDTO> UpdateAsync(UpdatePromotionProgramRequest request)
    {
        var entity = await _uow.PromotionProgramStore.GetByIdWithChildrenAsync(request.Id);
        if (entity == null)
        {
            throw new KeyNotFoundException($"PromotionProgram {request.Id} not found.");
        }

        await ValidateAsync(request.Mechanic, request.PercentOff, request.Eligibility, request.MinMemberShipId, request.MaxAge,
            request.StartDate, request.EndDate, request.Prices, request.Schedules, request.AutoApply, request.Code,
            request.MaxTicketsPerTransaction, request.MovieId, request.Id);

        entity.Title = request.Title.Trim();
        entity.Summary = Normalize(request.Summary);
        entity.BannerUrl = Normalize(request.BannerUrl);
        entity.Category = request.Category;
        entity.Mechanic = request.Mechanic;
        entity.Eligibility = request.Eligibility;
        entity.StartDate = request.StartDate;
        entity.EndDate = request.EndDate;
        entity.ExcludeHolidays = request.ExcludeHolidays;
        entity.AllowedChannelsMask = request.AllowedChannelsMask;
        entity.MaxTicketsPerTransaction = request.MaxTicketsPerTransaction;
        entity.StacksWithMembershipDiscount = request.StacksWithMembershipDiscount;
        entity.TermsText = Normalize(request.TermsText);
        entity.DisplayOrder = request.DisplayOrder;
        entity.IsPublished = request.IsPublished;
        entity.AutoApply = request.AutoApply;
        entity.Code = NormalizeCode(request.Code);
        entity.MaxUsage = request.MaxUsage;
        entity.MaxDiscountAmount = request.MaxDiscountAmount;
        entity.MovieId = request.MovieId;
        entity.LastUpdatedTime = DateTime.UtcNow;
        ApplyMechanicAndEligibility(entity, request.PercentOff, request.MinMemberShipId, request.MaxAge, request.Prices);
        ReplaceSchedules(entity, request.Schedules);

        await _uow.SaveChangesAsync();
        var dtos = await MapPageAsync(new List<PromotionProgram> { entity });
        return dtos[0];
    }

    public async Task DeleteAsync(Guid id)
    {
        await _uow.PromotionProgramStore.DeleteAsync(id);
    }

    // ── Validation ───────────────────────────────────────────────────────────

    private async Task ValidateAsync(
        PromotionMechanic mechanic, double? percentOff,
        PromotionEligibility eligibility, Guid? minMemberShipId, int? maxAge,
        DateTime startDate, DateTime endDate,
        List<PromotionProgramPriceDTO> prices, List<PromotionProgramScheduleDTO> schedules,
        bool autoApply, string? code, int? maxTicketsPerTransaction, Guid? movieId, Guid? selfId)
    {
        if (endDate < startDate)
        {
            throw new InvalidOperationException("End date must not be before start date.");
        }

        switch (mechanic)
        {
            case PromotionMechanic.FixedPrice:
                if (prices == null || prices.Count == 0)
                {
                    throw new InvalidOperationException("FixedPrice requires at least one price row.");
                }
                if (prices.Any(p => p.Price < 0))
                {
                    throw new InvalidOperationException("Price rows must not be negative.");
                }
                if (prices.Any(p => p.SeatKind == SeatKind.Aisle))
                {
                    throw new InvalidOperationException("Aisle is not a priced seat kind.");
                }
                if (prices.GroupBy(p => (p.ProjectionForm, p.SeatKind)).Any(g => g.Count() > 1))
                {
                    throw new InvalidOperationException("Duplicate price rows for the same format/seat kind.");
                }
                break;
            case PromotionMechanic.PercentOff:
                if (!(percentOff > 0) || percentOff > 100)
                {
                    throw new InvalidOperationException("PercentOff must be between 0 (exclusive) and 100.");
                }
                break;
        }

        switch (eligibility)
        {
            case PromotionEligibility.Members:
                if (minMemberShipId is not Guid tierId || !await _uow.MemberShipStore.ExistsAsync(m => m.Id == tierId))
                {
                    throw new InvalidOperationException("A valid membership tier is required for Members eligibility.");
                }
                break;
            case PromotionEligibility.AgeLimit:
                if (!(maxAge > 0))
                {
                    throw new InvalidOperationException("A positive MaxAge is required for AgeLimit eligibility.");
                }
                break;
        }

        foreach (var s in schedules ?? new())
        {
            var hasStart = !string.IsNullOrWhiteSpace(s.StartTimeOfDay);
            var hasEnd = !string.IsNullOrWhiteSpace(s.EndTimeOfDay);
            if (hasStart != hasEnd)
            {
                throw new InvalidOperationException("A schedule row needs both a start and end time, or neither.");
            }
            if (hasStart && hasEnd)
            {
                var start = ParseTime(s.StartTimeOfDay);
                var end = ParseTime(s.EndTimeOfDay);
                if (start is null || end is null || start >= end)
                {
                    throw new InvalidOperationException("A schedule row's start time must be before its end time.");
                }
            }
        }

        if (maxTicketsPerTransaction <= 0)
        {
            throw new InvalidOperationException("MaxTicketsPerTransaction must be positive when set.");
        }

        if (movieId is Guid mId && !await _uow.MovieStore.ExistsAsync(m => m.Id == mId))
        {
            throw new InvalidOperationException("Selected movie does not exist.");
        }

        var normalizedCode = NormalizeCode(code);
        if (autoApply && normalizedCode is not null)
        {
            throw new InvalidOperationException("A program cannot both require a code and auto-apply.");
        }

        if (normalizedCode is not null
            && await _uow.PromotionProgramStore.ExistsAsync(p => p.Code == normalizedCode && (selfId == null || p.Id != selfId)))
        {
            throw new InvalidOperationException("This code is already used by another program.");
        }

        if (autoApply)
        {
            if (mechanic == PromotionMechanic.Info)
            {
                throw new InvalidOperationException("Info programs cannot be set to auto-apply.");
            }
            if (eligibility is PromotionEligibility.StudentOrTeacher or PromotionEligibility.AgeLimit)
            {
                throw new InvalidOperationException("StudentOrTeacher/AgeLimit eligibility cannot be auto-applied online (no verification available) — leave AutoApply off.");
            }
        }
    }

    // ── Entity shaping ───────────────────────────────────────────────────────

    /// <summary>Clears fields that don't apply to the chosen mechanic/eligibility, and replaces the
    /// price-row collection wholesale.
    /// A new row is created with its PromotionProgramId set explicitly and handed to
    /// IPromotionProgramStore.AddPrice (an explicit DbSet.Add), not entity.Prices.Add(...): a child
    /// discovered only via collection-navigation fixup, never through an explicit Add(), gets tracked
    /// as Modified instead of Added once its Guid Id is a non-default, client-set value (BaseEntity
    /// sets Id = Guid.NewGuid() in its property initializer) — EF's heuristic for navigation-discovered
    /// entities with an already-set key assumes "this might reference an existing row" rather than
    /// "this is new", which threw DbUpdateConcurrencyException ("expected 1 row, affected 0") on every
    /// Update, even a no-op one, because the "Modified" UPDATE targeted a row that had never existed.
    /// The old rows are explicitly removed first (RemovePrices) so they are never fixed-up against the
    /// new ones, and entity.Prices is still kept in sync for the in-memory graph MapPageAsync reads
    /// afterwards.</summary>
    private void ApplyMechanicAndEligibility(PromotionProgram entity, double? percentOff, Guid? minMemberShipId, int? maxAge, List<PromotionProgramPriceDTO> prices)
    {
        entity.PercentOff = entity.Mechanic == PromotionMechanic.PercentOff ? percentOff : null;
        entity.MinMemberShipId = entity.Eligibility == PromotionEligibility.Members ? minMemberShipId : null;
        entity.MaxAge = entity.Eligibility == PromotionEligibility.AgeLimit ? maxAge : null;

        _uow.PromotionProgramStore.RemovePrices(entity.Prices);
        entity.Prices = new List<PromotionProgramPrice>();
        if (entity.Mechanic == PromotionMechanic.FixedPrice)
        {
            foreach (var p in prices ?? new())
            {
                var price = new PromotionProgramPrice { PromotionProgramId = entity.Id, ProjectionForm = p.ProjectionForm, SeatKind = p.SeatKind, Price = p.Price };
                _uow.PromotionProgramStore.AddPrice(price);
                entity.Prices.Add(price);
            }
        }
    }

    /// <summary>See the remarks on <see cref="ApplyMechanicAndEligibility"/> — same fix, for Schedules.</summary>
    private void ReplaceSchedules(PromotionProgram entity, List<PromotionProgramScheduleDTO> schedules)
    {
        _uow.PromotionProgramStore.RemoveSchedules(entity.Schedules);
        entity.Schedules = new List<PromotionProgramSchedule>();
        foreach (var s in schedules ?? new())
        {
            var schedule = new PromotionProgramSchedule
            {
                PromotionProgramId = entity.Id,
                TheaterId = s.TheaterId,
                DaysOfWeekMask = s.DaysOfWeekMask,
                StartTimeOfDay = ParseTime(s.StartTimeOfDay),
                EndTimeOfDay = ParseTime(s.EndTimeOfDay),
            };
            _uow.PromotionProgramStore.AddSchedule(schedule);
            entity.Schedules.Add(schedule);
        }
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string? NormalizeCode(string? code) => string.IsNullOrWhiteSpace(code) ? null : code.Trim();
    private static TimeOnly? ParseTime(string? value) => TimeOnly.TryParse(value, out var t) ? t : null;

    // ── Mapping (fixed number of queries regardless of page size) ────────────

    private async Task<List<PromotionProgramDTO>> MapPageAsync(List<PromotionProgram> items)
    {
        var dtos = items.Select(p => p.ToDTO<PromotionProgram, PromotionProgramDTO>()).ToList();
        if (dtos.Count == 0)
        {
            return dtos;
        }

        var byId = items.ToDictionary(p => p.Id);
        var ids = items.Select(p => p.Id).ToList();

        var pricesByProgram = (await _uow.PromotionProgramStore.GetPricesAsync(ids)).ToLookup(x => x.PromotionProgramId);
        var schedulesByProgram = (await _uow.PromotionProgramStore.GetSchedulesAsync(ids)).ToLookup(x => x.PromotionProgramId);

        var theaterIds = schedulesByProgram.SelectMany(g => g).Where(s => s.TheaterId.HasValue).Select(s => s.TheaterId!.Value).Distinct().ToList();
        var theaterNames = theaterIds.Count > 0
            ? (await _uow.TheaterStore.FindAsync(t => theaterIds.Contains(t.Id))).ToDictionary(t => t.Id, t => t.Name)
            : new Dictionary<Guid, string>();

        var tierIds = items.Where(p => p.MinMemberShipId.HasValue).Select(p => p.MinMemberShipId!.Value).Distinct().ToList();
        var tierNames = tierIds.Count > 0
            ? (await _uow.MemberShipStore.FindAsync(m => tierIds.Contains(m.Id))).ToDictionary(m => m.Id, m => m.Name)
            : new Dictionary<Guid, string>();

        var movieIds = items.Where(p => p.MovieId.HasValue).Select(p => p.MovieId!.Value).Distinct().ToList();
        var movieTitles = movieIds.Count > 0
            ? (await _uow.MovieStore.FindAsync(m => movieIds.Contains(m.Id))).ToDictionary(m => m.Id, m => m.Title)
            : new Dictionary<Guid, string>();

        foreach (var dto in dtos)
        {
            var entity = byId[dto.Id];

            // A just-created/updated entity's own navigation collections are already authoritative in
            // memory (no round trip needed); a list-page entity from GetQuery() never has them loaded
            // (always empty), so those fall back to the bulk-fetched rows above.
            IEnumerable<PromotionProgramPrice> priceRows = entity.Prices.Count > 0 ? entity.Prices : pricesByProgram[entity.Id];
            dto.Prices = priceRows
                .Select(p => new PromotionProgramPriceDTO { ProjectionForm = p.ProjectionForm, SeatKind = p.SeatKind, Price = p.Price })
                .ToList();

            IEnumerable<PromotionProgramSchedule> scheduleRows = entity.Schedules.Count > 0 ? entity.Schedules : schedulesByProgram[entity.Id];
            dto.Schedules = scheduleRows.Select(s => new PromotionProgramScheduleDTO
            {
                TheaterId = s.TheaterId,
                TheaterName = s.TheaterId.HasValue ? theaterNames.GetValueOrDefault(s.TheaterId.Value) : null,
                DaysOfWeekMask = s.DaysOfWeekMask,
                StartTimeOfDay = s.StartTimeOfDay?.ToString("HH:mm"),
                EndTimeOfDay = s.EndTimeOfDay?.ToString("HH:mm"),
            }).ToList();

            if (entity.MinMemberShipId.HasValue)
            {
                dto.MinMemberShipName = tierNames.GetValueOrDefault(entity.MinMemberShipId.Value);
            }

            if (entity.MovieId.HasValue)
            {
                dto.MovieTitle = movieTitles.GetValueOrDefault(entity.MovieId.Value);
            }
        }

        return dtos;
    }
}
