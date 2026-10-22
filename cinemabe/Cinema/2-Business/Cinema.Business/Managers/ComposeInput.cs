using Cinema.Business.DTO.Booking;

namespace Cinema.Business.Managers;

internal sealed class ComposeInput
{
    public Guid? CustomerUserId { get; init; }
    /// <summary>The theater the sale happens in. Null for an online booking: derived from the showtime's room.</summary>
    public Guid? TheaterId { get; init; }
    public Guid? ShowTimeId { get; init; }
    public Guid? RoomId { get; init; }
    public IReadOnlyList<BookingSeatItem> Seats { get; init; } = Array.Empty<BookingSeatItem>();
    public IReadOnlyList<BookingFoodItem> Foods { get; init; } = Array.Empty<BookingFoodItem>();
    public string? DiscountCode { get; init; }
    public int PointsToRedeem { get; init; }
    public string? GiftCardCode { get; init; }
    public string? ConnectionId { get; init; }
    /// <summary>Counter sale: whole-VND amounts, the showtime must be sellable and belong to <see cref="TheaterId"/>.</summary>
    public bool IsCounter { get; init; }
    /// <summary>Quote: run every check but write nothing (no stock, points or gift-card change, no invoice).</summary>
    public bool DryRun { get; init; }
    public IReadOnlyDictionary<Guid, double> SeatPriceOverrides { get; init; } = new Dictionary<Guid, double>();
    public IReadOnlyDictionary<Guid, double> FoodPriceOverrides { get; init; } = new Dictionary<Guid, double>();
}
