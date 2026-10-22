using System.ComponentModel.DataAnnotations;
using Cinema.Business.DTO.Staff;
using Cinema.Business.DTO.Validation;

namespace Cinema.Business.DTO.BoxOffice;

public class CounterSeatItem
{
    [NotEmptyGuid]
    public Guid SeatId { get; set; }

    /// <summary>Patron category (ticket type) for this seat. Required, same rule as online booking.</summary>
    public Guid? PatronCategoryId { get; set; }

    /// <summary>Manager-approved price for this ticket row (a double seat is two rows). Needs a manager override;
    /// must be between 0 and the list price. Null = list price.</summary>
    [Range(0, 100000000)]
    public double? OverrideUnitPrice { get; set; }
}
