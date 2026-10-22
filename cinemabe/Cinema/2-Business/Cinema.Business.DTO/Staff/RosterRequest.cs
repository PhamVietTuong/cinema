using System.ComponentModel.DataAnnotations;

namespace Cinema.Business.DTO.Staff;

public class RosterRequest
{
    /// <summary>Required for admins and multi-theater callers; staff may omit it (their own theater is used).</summary>
    public Guid? TheaterId { get; set; }
    public DateTime From { get; set; }
    /// <summary>Exclusive end of the range; at most 31 days after <see cref="From"/>.</summary>
    public DateTime To { get; set; }
    public Guid? UserId { get; set; }
}
