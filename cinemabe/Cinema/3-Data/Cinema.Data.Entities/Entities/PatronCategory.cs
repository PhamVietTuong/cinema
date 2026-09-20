namespace Cinema.Data.Entities;

/// <summary>
/// A per-theater, per-seat-kind pricing row (e.g. Adult/Standard, Adult/Double, Student/Standard).
/// A logical category such as "Adult" is represented as one row PER SeatKind it may book — omitting a
/// row for a kind means that category cannot book that kind of seat at all (e.g. no Student/Double row
/// means Students cannot book double seats). This is the entire eligibility rule: there is no separate
/// allow-list table. Price is absolute VND, configured directly and independently per row — a Double
/// row's price is NOT computed from the Standard row (a couple seat may cost more than 2x standard for
/// the seating privacy, so an admin sets it explicitly).
/// The API models this per seat (CreateBookingRequest.BookingSeatItem.PatronCategoryId) so a single
/// order can mix categories (e.g. 2 Adult + 2 Child), mirroring how Vietnamese chains sell tickets.
/// </summary>
public class PatronCategory : BaseEntity
{
    public Guid TheaterId { get; set; }
    public Theater Theater { get; set; } = null!;

    public Guid SeatTypeId { get; set; }
    public SeatType SeatType { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Absolute price in VND for one seat row of this kind, before RoomType overrides.</summary>
    public double Price { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<RoomTypePatronCategoryPrice> RoomTypePrices { get; set; } = new List<RoomTypePatronCategoryPrice>();
}
