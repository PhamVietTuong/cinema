
namespace Cinema.Business.DTO.Staff;

// ── Roster ───────────────────────────────────────────────────────────────────

public class GetTheaterStaffRequest
{
    /// <summary>Required for admins and multi-theater callers; staff may omit it (their own theater is used).</summary>
    public Guid? TheaterId { get; set; }
}
