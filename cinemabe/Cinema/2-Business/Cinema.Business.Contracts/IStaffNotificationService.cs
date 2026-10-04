using Cinema.Business.DTO.Concession;
using Cinema.Business.DTO.Operations;

namespace Cinema.Business.Contracts;

/// <summary>
/// Pushes real-time events to the staff app (<c>StaffHub</c>, groups <c>theater:{id}</c>). The dev default is a no-op
/// (logs only); the Web API host wires in a SignalR-backed implementation. Implementations must never throw: a
/// notification failure can't be allowed to fail an already-committed sale or status change.
/// </summary>
public interface IStaffNotificationService
{
    /// <summary>Event <c>FoodOrderQueued</c>: a paid invoice with food entered the pickup queue.</summary>
    Task NotifyFoodOrderQueuedAsync(Guid theaterId, PickupOrderDTO order);

    /// <summary>Event <c>FoodOrderUpdated</c>: the pickup status of an invoice changed.</summary>
    Task NotifyFoodOrderUpdatedAsync(Guid theaterId, FoodOrderUpdateDTO update);

    /// <summary>Event <c>StockLow</c>: items whose stock crossed their low-stock threshold during a sale.</summary>
    Task NotifyStockLowAsync(Guid theaterId, IReadOnlyList<LowStockItemDTO> items);

    /// <summary>Event <c>IncidentRaised</c>: a staff incident was reported.</summary>
    Task NotifyIncidentRaisedAsync(Guid theaterId, IncidentDTO incident);
}
