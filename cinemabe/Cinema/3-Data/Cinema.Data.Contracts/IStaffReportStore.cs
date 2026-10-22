namespace Cinema.Data.Contracts;

/// <summary>Read-only management reports. Every method issues a fixed number of queries regardless of row count.</summary>
public interface IStaffReportStore
{
    /// <summary>Sales grouped by <see cref="SalesQuery.GroupBy"/>: at most 6 grouped queries.</summary>
    Task<SalesAggregates> GetSalesAsync(SalesQuery query);

    /// <summary>Movie titles by id (one query). Unknown ids are absent.</summary>
    Task<Dictionary<Guid, string>> GetMovieTitlesAsync(IReadOnlyCollection<Guid> ids);

    /// <summary>Theater names by id (one query). Unknown ids are absent.</summary>
    Task<Dictionary<Guid, string>> GetTheaterNamesAsync(IReadOnlyCollection<Guid> ids);

    /// <summary>Per showtime-room sold/active seats: 3 queries (screenings, sold tickets, active seats).</summary>
    Task<List<OccupancyRow>> GetOccupancyAsync(OccupancyQuery query);

    /// <summary>The KPI building blocks: 4 queries.</summary>
    Task<KpiRaw> GetKpiRawAsync(SalesQuery query);
}
