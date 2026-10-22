using System.ComponentModel.DataAnnotations;
using Cinema.Business.DTO.Staff;

namespace Cinema.Business.DTO.Operations;

public class ResolveIncidentRequest
{
    public Guid IncidentId { get; set; }

    [StringLength(1000)]
    public string? ResolutionNote { get; set; }

    /// <summary>Also put the blocked seat/room back on sale. Needs an approver (or an override).</summary>
    public bool Unblock { get; set; }

    /// <summary>Manager approval when the caller is not an approver and <see cref="Unblock"/> is set.</summary>
    public ManagerOverrideDTO? Override { get; set; }
}
