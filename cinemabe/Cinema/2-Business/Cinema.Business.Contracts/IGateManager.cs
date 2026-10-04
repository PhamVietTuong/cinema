using Cinema.Business.DTO.Gate;

namespace Cinema.Business.Contracts;

/// <summary>Gate (theater entrance) ticket validation for the staff app.</summary>
public interface IGateManager
{
    /// <summary>Decides a scan and, when admissible, atomically marks the ticket used. Rejections are RETURNED as
    /// an outcome, never thrown.</summary>
    Task<ScanTicketResultDTO> ScanAsync(Guid theaterId, Guid userId, ScanTicketRequest request);

    /// <summary>Finds today's paid tickets of the theater by invoice code and/or phone (at least one required,
    /// else <see cref="InvalidOperationException"/>).</summary>
    Task<List<GateLookupResultDTO>> LookupAsync(Guid theaterId, GateLookupRequest request);
}
