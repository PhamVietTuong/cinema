namespace Cinema.Data.Entities;
public class SeatType : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Color { get; set; } = "#808080";
    public ICollection<Seat> Seats { get; set; } = new List<Seat>();
    public ICollection<SeatTypeTicketType> SeatTypeTicketTypes { get; set; } = new List<SeatTypeTicketType>();
}
