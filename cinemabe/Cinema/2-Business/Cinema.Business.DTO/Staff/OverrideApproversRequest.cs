using Cinema.Data.Enums;

namespace Cinema.Business.DTO.Staff;

public class OverrideApproversRequest
{
    /// <summary>Required for admins; staff may omit it (their own theater is used).</summary>
    public Guid? TheaterId { get; set; }
}
