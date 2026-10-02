using Cinema.Data.Enums;
namespace Cinema.Business.DTO.Booking;
public class SeatDTO
{
    public Guid Id { get; set; }
    public string RowName { get; set; } = string.Empty;
    public int ColIndex { get; set; }
    /// <summary>Derived from SeatGroupId being set.</summary>
    public bool IsDouble { get; set; }
    public SeatStatus Status { get; set; }

    /// <summary>The cheapest resolved price across active PatronCategory rows matching this seat's
    /// kind — a "from" figure for the seat map, not what a specific booking will actually charge
    /// (that depends on the category chosen at booking time). 0 when no category matches this kind
    /// at this theater (the seat is currently unbookable).</summary>
    public double Price { get; set; }

    public bool IsLocked { get; set; }

    /// <summary>Set when this seat is part of a linked group (e.g. a double seat). Both
    /// seats in a group share the same id and must be selected/booked together.</summary>
    public Guid? SeatGroupId { get; set; }
}
