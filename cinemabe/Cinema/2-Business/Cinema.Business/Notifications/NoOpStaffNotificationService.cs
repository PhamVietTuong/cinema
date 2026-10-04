using Cinema.Business.Contracts;
using Cinema.Business.DTO.Concession;
using Cinema.Business.DTO.Operations;
using Cinema.Foundation.Logging;

namespace Cinema.Business.Notifications;

/// <summary>
/// Development default: logs instead of broadcasting, so the managers and the seeder/test exe run without SignalR.
/// Replaced by the Web API host's SignalR-backed sender at startup.
/// </summary>
public class NoOpStaffNotificationService : IStaffNotificationService
{
    public Task NotifyFoodOrderQueuedAsync(Guid theaterId, PickupOrderDTO order)
    {
        LogProvider.Current.Information($"[StaffNotification:skipped] FoodOrderQueued TheaterId={theaterId} Invoice={order.InvoiceCode}");
        return Task.CompletedTask;
    }

    public Task NotifyFoodOrderUpdatedAsync(Guid theaterId, FoodOrderUpdateDTO update)
    {
        LogProvider.Current.Information($"[StaffNotification:skipped] FoodOrderUpdated TheaterId={theaterId} Invoice={update.InvoiceCode} Status={update.FoodStatus}");
        return Task.CompletedTask;
    }

    public Task NotifyStockLowAsync(Guid theaterId, IReadOnlyList<LowStockItemDTO> items)
    {
        LogProvider.Current.Information($"[StaffNotification:skipped] StockLow TheaterId={theaterId} Items={string.Join(",", items.Select(i => i.Name))}");
        return Task.CompletedTask;
    }

    public Task NotifyIncidentRaisedAsync(Guid theaterId, IncidentDTO incident)
    {
        LogProvider.Current.Information($"[StaffNotification:skipped] IncidentRaised TheaterId={theaterId} IncidentId={incident.Id}");
        return Task.CompletedTask;
    }
}
