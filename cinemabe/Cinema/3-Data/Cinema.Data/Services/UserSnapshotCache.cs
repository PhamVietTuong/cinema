using Microsoft.Extensions.Caching.Memory;

namespace Cinema.Data.Services;

/// <summary>
/// Short-lived, process-local cache of the per-user account snapshot the API re-checks on every authenticated
/// request (see the host's TokenRevalidator), so a burst of requests costs one query instead of one each.
/// <see cref="Contexts.CinemaContext"/> evicts a user's entry whenever that user row is saved or deleted, so a
/// ban, role/theater change or password change still takes effect immediately; the TTL is only a backstop for
/// changes made outside the app (e.g. direct SQL or a UserType rename).
/// </summary>
public static class UserSnapshotCache
{
    public static readonly TimeSpan Ttl = TimeSpan.FromSeconds(20);

    private static readonly MemoryCache _cache = new(new MemoryCacheOptions { SizeLimit = 10_000 });

    public static bool TryGet(Guid userId, out object? snapshot)
    {
        return _cache.TryGetValue(userId, out snapshot);
    }

    public static void Set(Guid userId, object snapshot)
    {
        _cache.Set(userId, snapshot, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = Ttl, Size = 1 });
    }

    public static void Evict(Guid userId)
    {
        _cache.Remove(userId);
    }
}
