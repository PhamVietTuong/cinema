namespace Cinema.Data.Entities;

/// <summary>A named time-of-day range (per theater) used as a ticket-pricing dimension.</summary>
public class TimeSlot : BaseEntity
{
    public Guid TheaterId { get; set; }
    public Theater Theater { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    /// <summary>Start of the range, "HH:mm" (inclusive).</summary>
    public string StartTime { get; set; } = string.Empty;
    /// <summary>End of the range, "HH:mm" (exclusive).</summary>
    public string EndTime { get; set; } = string.Empty;

    public ICollection<TicketPrice> TicketPrices { get; set; } = new List<TicketPrice>();
}
