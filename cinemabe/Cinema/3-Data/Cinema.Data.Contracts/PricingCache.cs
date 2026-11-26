using Cinema.Data.Entities;

namespace Cinema.Data.Contracts;

/// <summary>
/// Version stamp for the cached per-(showtime, room) seat pricing context. Any save/delete of an entity that feeds
/// the price (holiday, ticket-price matrix, time slot, patron category and its room-type prices, room type, seat
/// type, room, showtime, showtime-room) bumps <see cref="Version"/>, which is part of the cache key — so an admin
/// pricing change is visible immediately and stale entries simply expire. The data layer calls
/// <see cref="Invalidate"/>; the booking manager reads <see cref="Version"/>.
/// </summary>
public static class PricingCache
{
    private static int _version;

    private static readonly HashSet<Type> _pricingEntities = new()
    {
        typeof(Holiday), typeof(TicketPrice), typeof(TimeSlot), typeof(PatronCategory),
        typeof(RoomTypePatronCategoryPrice), typeof(RoomType), typeof(SeatType), typeof(Room),
        typeof(ShowTime), typeof(ShowTimeRoom),
    };

    public static int Version => Volatile.Read(ref _version);

    public static void Invalidate()
    {
        Interlocked.Increment(ref _version);
    }

    public static bool Affects(Type entityType)
    {
        return _pricingEntities.Contains(entityType);
    }
}
