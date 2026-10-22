using System.ComponentModel.DataAnnotations;
using Cinema.Business.DTO.Validation;

namespace Cinema.Business.DTO.Booking;

public class BookingSeatItem
{
    [NotEmptyGuid]
    public Guid SeatId { get; set; }

    /// <summary>Self-reported patron category for this seat (Adult/Student/Senior/Child) — REQUIRED,
    /// since PatronCategory.Price is now the only source of a ticket's price. Checked visually
    /// (ID/student card) at the theater, not verified by this system. The category's own seat kind
    /// (Standard/Double) must match the seat being booked, or the booking is rejected — this is the
    /// entire eligibility rule (a category with no row for a kind simply cannot book that kind).</summary>
    public Guid? PatronCategoryId { get; set; }
}
