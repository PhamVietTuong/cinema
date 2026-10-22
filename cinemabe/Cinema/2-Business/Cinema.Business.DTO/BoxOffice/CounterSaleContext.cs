using System.ComponentModel.DataAnnotations;
using Cinema.Business.DTO.Booking;
using Cinema.Business.DTO.Staff;
using Cinema.Business.DTO.Validation;
using Cinema.Data.Enums;

namespace Cinema.Business.DTO.BoxOffice;

/// <summary>Everything the booking core needs to ring up a counter sale (resolved by <c>BoxOfficeManager</c>).</summary>
public class CounterSaleContext
{
    public Guid TheaterId { get; set; }
    public Guid StaffUserId { get; set; }
    /// <summary>Approver of the price overrides (the staff member themself when they are an approver).</summary>
    public Guid? ApproverUserId { get; set; }
    public CounterSaleRequest Request { get; set; } = new();
}
