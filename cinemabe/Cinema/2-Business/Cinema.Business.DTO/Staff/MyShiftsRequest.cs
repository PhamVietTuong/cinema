using System.ComponentModel.DataAnnotations;

namespace Cinema.Business.DTO.Staff;

public class MyShiftsRequest
{
    public DateTime From { get; set; }
    /// <summary>Exclusive end of the range; at most 31 days after <see cref="From"/>.</summary>
    public DateTime To { get; set; }
}
