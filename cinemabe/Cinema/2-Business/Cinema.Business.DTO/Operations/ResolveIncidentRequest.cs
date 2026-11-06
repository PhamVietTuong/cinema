using System.ComponentModel.DataAnnotations;

namespace Cinema.Business.DTO.Operations;

public class ResolveIncidentRequest
{
    public Guid IncidentId { get; set; }

    [StringLength(1000)]
    public string? ResolutionNote { get; set; }

    /// <summary>Also put the blocked seat/room back on sale. Needs an Approver.</summary>
    public bool Unblock { get; set; }
}
