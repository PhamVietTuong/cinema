namespace Cinema.Data.Entities;

/// <summary>
/// A per-theater seat kind: exactly two rows per theater, Standard and Double (couple seat), matched
/// by <see cref="Kind"/> — never by <see cref="Name"/>, which is display text only and may be renamed.
/// Carries no pricing; pricing lives on <see cref="PatronCategory"/> instead.
/// </summary>
public class SeatType : BaseEntity
{
    public Guid TheaterId { get; set; }
    public Theater Theater { get; set; } = null!;

    public SeatKind Kind { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Color { get; set; } = "#808080";

    public ICollection<PatronCategory> PatronCategories { get; set; } = new List<PatronCategory>();
}
