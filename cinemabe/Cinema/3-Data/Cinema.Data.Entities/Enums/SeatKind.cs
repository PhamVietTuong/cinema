namespace Cinema.Data.Entities;

public enum SeatKind
{
    Standard = 0,
    Double = 1,

    /// <summary>
    /// A non-bookable gap cell (aisle/walkway). Valid only on <see cref="Seat.Kind"/> —
    /// never a SeatType, PatronCategory, or promotion-program seat kind.
    /// </summary>
    Aisle = 2
}
