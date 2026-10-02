using Cinema.Data.Entities;

namespace Cinema.Business.DTO.Booking;

/// <summary>The price list for a specific showtime+room: one entry per active PatronCategory of the
/// theater, with all pricing factors (RoomType override, TimeSlot/Holiday multiplier, 3D surcharge,
/// per-showtime BasePrice surcharge) already resolved. Drives the CinemaUser quantity picker and per-
/// seat totals directly — the client does no pricing math of its own.</summary>
public class ShowTimePriceDTO
{
    public Guid PatronCategoryId { get; set; }
    public string PatronCategoryName { get; set; } = string.Empty;
    public Guid SeatTypeId { get; set; }
    public SeatKind Kind { get; set; }
    public bool IsDouble { get; set; }

    /// <summary>Final price for one seat row of this kind under this category, for this showtime.</summary>
    public double Price { get; set; }
}
