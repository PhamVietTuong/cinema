using Cinema.Business.DTO.Staff;
using Cinema.Data.Enums;

namespace Cinema.Business.DTO.Operations;

// ── Schedule board ───────────────────────────────────────────────────────────

public class ScheduleBoardRequest
{
    /// <summary>Required for admins and multi-theater callers; staff may omit it (their own theater is used).</summary>
    public Guid? TheaterId { get; set; }

    /// <summary>The day to show (only the date part is used).</summary>
    public DateTime Date { get; set; }
}
