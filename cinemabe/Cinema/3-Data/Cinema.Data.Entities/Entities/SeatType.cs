namespace Cinema.Data.Entities;
public class SeatType : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Color { get; set; } = "#808080";

    /// <summary>
    /// Multiplier applied to a showtime's base price for seats of this type
    /// (e.g. 1.0 = standard, 1.5 = VIP, 2.0 = double). Replaces the old
    /// per-ticket-type pricing matrix.
    /// </summary>
    public double PriceMultiplier { get; set; } = 1;

    public ICollection<Seat> Seats { get; set; } = new List<Seat>();
}
