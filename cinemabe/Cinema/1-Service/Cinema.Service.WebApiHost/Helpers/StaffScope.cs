using Cinema.Business.Contracts.Exceptions;

namespace Cinema.Service.WebApiHost.Helpers;

/// <summary>
/// The theaters a staff caller may act on. Admins are <see cref="IsAll"/> (every theater, but must name one per
/// request). Theater-scoped roles carry their theater, hence a collection.
/// </summary>
public sealed class StaffScope
{
    public bool IsAll { get; }
    public IReadOnlyCollection<Guid> TheaterIds { get; }

    public StaffScope(bool isAll, IReadOnlyCollection<Guid> theaterIds)
    {
        IsAll = isAll;
        TheaterIds = theaterIds;
    }

    /// <summary>
    /// Picks the theater a request acts on. A theater inside the scope is returned as is; one outside it throws
    /// <see cref="AccessDeniedException"/> (403, never 401). An omitted theater defaults to the caller's only
    /// theater; admins and multi-theater callers must pass one explicitly (<see cref="InvalidOperationException"/>, 400).
    /// </summary>
    public Guid Resolve(Guid? requestedTheaterId)
    {
        var requested = requestedTheaterId == Guid.Empty ? null : requestedTheaterId;
        if (requested == null)
        {
            if (!IsAll && TheaterIds.Count == 1)
            {
                return TheaterIds.First();
            }
            throw new InvalidOperationException("A theater must be specified.");
        }

        if (IsAll || TheaterIds.Contains(requested.Value))
        {
            return requested.Value;
        }
        throw new AccessDeniedException("The requested theater is outside your scope.");
    }

    /// <summary>The theater filter for list/report queries: null = all theaters (admin), otherwise the scoped ids.</summary>
    public IReadOnlyCollection<Guid>? ToTheaterFilter()
    {
        return IsAll ? null : TheaterIds;
    }
}
