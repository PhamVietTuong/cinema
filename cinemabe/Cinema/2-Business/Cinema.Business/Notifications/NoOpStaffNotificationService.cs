using Cinema.Business.Contracts;
using Cinema.Business.DTO.Operations;
using Cinema.Foundation.Logging;

namespace Cinema.Business.Notifications;

/// <summary>
/// Development default: logs instead of broadcasting, so the managers and the seeder/test exe run without SignalR.
/// Replaced by the Web API host's SignalR-backed sender at startup.
/// </summary>
public class NoOpStaffNotificationService : IStaffNotificationService
{
    public Task NotifyIncidentRaisedAsync(Guid theaterId, IncidentDTO incident)
    {
        LogProvider.Current.Information($"[StaffNotification:skipped] IncidentRaised TheaterId={theaterId} IncidentId={incident.Id}");
        return Task.CompletedTask;
    }
}
