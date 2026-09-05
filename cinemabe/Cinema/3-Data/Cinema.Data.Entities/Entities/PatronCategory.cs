namespace Cinema.Data.Entities;

/// <summary>
/// A per-theater patron pricing category (e.g. Adult, Student, Senior, Child). DiscountPercent
/// reduces a ticket's own price; it is self-reported by the customer and checked visually
/// (ID/student card) at the theater, not verified by this system.
/// The API models this per seat (CreateBookingRequest.BookingSeatItem.PatronCategoryId) so a single
/// order can mix categories (e.g. 2 Adult + 2 Child), mirroring how Vietnamese chains sell tickets.
/// The CinemaUser web app has the customer pick a QUANTITY per category up front, building one
/// "ticket slot" per ticket; each seat click claims the most category-appropriate free slot. See
/// AllowedSeatTypes/PatronCategorySeatType, which gates seat *type* selectability by category and is
/// what motivated putting the quantity picker before the seat map in that UI.
/// </summary>
public class PatronCategory : BaseEntity
{
    public Guid TheaterId { get; set; }
    public Theater Theater { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Percent off this category's ticket price (0 = full price).</summary>
    public double DiscountPercent { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<PatronCategorySeatType> AllowedSeatTypes { get; set; } = new List<PatronCategorySeatType>();
}
