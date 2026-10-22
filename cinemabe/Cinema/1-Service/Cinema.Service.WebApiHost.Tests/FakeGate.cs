using Cinema.Business.Contracts;
using Cinema.Business.DTO.Gate;
using Cinema.Data.Enums;

namespace Cinema.Service.WebApiHost.Tests;

internal sealed class FakeGate : IGateManager
{
    public Guid? ScannedTheater { get; private set; }

    public Task<ScanTicketResultDTO> ScanAsync(Guid theaterId, Guid userId, ScanTicketRequest request)
    {
        ScannedTheater = theaterId;
        return Task.FromResult(new ScanTicketResultDTO { Outcome = ScanOutcome.AlreadyUsed });
    }

    public Task<List<GateLookupResultDTO>> LookupAsync(Guid theaterId, GateLookupRequest request)
    {
        return Task.FromResult(new List<GateLookupResultDTO>());
    }
}
