using Cinema.Business.Contracts;
using Cinema.Business.DTO.Operations;
using Cinema.Foundation.Logging;
using Microsoft.AspNetCore.SignalR;

namespace Cinema.Service.WebApiHost.Hubs;

/// <summary>
/// Broadcasts staff events to <see cref="StaffHub"/>'s <c>theater:{id}</c> group. Never throws: a broadcast failure
/// must not turn an already-committed sale, status change or incident into an error.
/// </summary>
public class SignalRStaffNotificationService : IStaffNotificationService
{
    private readonly IHubContext<StaffHub> _hub;

    public SignalRStaffNotificationService(IHubContext<StaffHub> hub)
    {
        _hub = hub;
    }

    public Task NotifyIncidentRaisedAsync(Guid theaterId, IncidentDTO incident)
    {
        return SendAsync(theaterId, StaffHub.IncidentRaisedEvent, incident);
    }

    private async Task SendAsync(Guid theaterId, string method, object payload)
    {
        try
        {
            await _hub.Clients.Group(StaffHub.TheaterGroup(theaterId)).SendAsync(method, payload);
        }
        catch (Exception e)
        {
            LogProvider.Current.Warning(e, $"{nameof(SignalRStaffNotificationService)}.{nameof(SendAsync)} {method} failed: {e.Message}");
        }
    }
}
