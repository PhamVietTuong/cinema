namespace Cinema.Data.Entities;

/// <summary>
/// A per-theater patron pricing category (e.g. Adult, Student, Senior, Child), chosen per seat at
/// checkout. DiscountPercent reduces that seat's own price; it is self-reported by the customer and
/// checked visually (ID/student card) at the theater, mirroring how Vietnamese chains sell mixed-
/// category tickets in one transaction.
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
}
