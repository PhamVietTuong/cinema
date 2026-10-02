namespace Cinema.Data.Entities;
public class ShowTimeRoom
{
    public Guid ShowTimeId { get; set; }
    public Guid RoomId { get; set; }

    /// <summary>Flat per-showtime price adjustment added to every ticket on this screening (0 = none).
    /// A manual lever for the admin to set per title/showtime — e.g. a premium for a new release,
    /// 0 for a long-running catalogue title — added on top of the resolved PatronCategory price. It is
    /// NOT the seat's base price; PatronCategory.Price is.</summary>
    public int BasePrice { get; set; }
    public ShowTime ShowTime { get; set; } = null!;
    public Room Room { get; set; } = null!;
    public ICollection<InvoiceTicket> InvoiceTickets { get; set; } = new List<InvoiceTicket>();
}
