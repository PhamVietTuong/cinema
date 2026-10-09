using Cinema.Business.Contracts;
using Cinema.Business.DTO.Concession;
using Cinema.Data.Contracts;
using Cinema.Data.Enums;
using Cinema.Foundation.Logging;

namespace Cinema.Business.Managers;

public class ConcessionManager : IConcessionManager
{
    private readonly IApplicationUnitOfWork _uow;
    private readonly IStaffNotificationService _notifications;
    private readonly TimeProvider _clock;

    public ConcessionManager(IApplicationUnitOfWork uow, IStaffNotificationService notifications, TimeProvider clock)
    {
        _uow = uow;
        _notifications = notifications;
        _clock = clock;
    }

    public async Task<List<PickupOrderDTO>> GetPickupQueueAsync(Guid theaterId, DateTime? day)
    {
        // Showtimes are stored in the theater's local time.
        var dayStart = (day ?? _clock.GetLocalNow().DateTime).Date;
        var rows = await _uow.InvoiceStore.GetPickupQueueAsync(theaterId, dayStart, dayStart.AddDays(1));

        return rows
            .OrderBy(r => r.ShowTimeStart.HasValue ? 0 : 1)
            .ThenBy(r => r.ShowTimeStart)
            .ThenBy(r => r.PaidAt)
            .Select(ToDto)
            .ToList();
    }

    public async Task<PickupOrderDTO> SetFoodStatusAsync(Guid theaterId, Guid staffId, Guid invoiceId, FoodOrderStatus next)
    {
        var header = await _uow.InvoiceStore.GetFoodOrderHeaderAsync(invoiceId);
        if (header == null || header.TheaterId != theaterId || header.FoodStatus == FoodOrderStatus.None)
        {
            throw new KeyNotFoundException("Food order not found.");
        }
        if (header.InvoiceStatus != InvoiceStatus.Paid)
        {
            throw new InvalidOperationException("Only a paid order can be prepared or handed over.");
        }
        if (!IsAllowedMove(header.FoodStatus, next))
        {
            throw new InvalidOperationException($"A food order cannot move from {header.FoodStatus} to {next}.");
        }

        var nowUtc = _clock.GetUtcNow().UtcDateTime;
        if (!await _uow.InvoiceStore.TrySetFoodStatusAsync(invoiceId, header.FoodStatus, next, staffId, nowUtc))
        {
            // A colleague moved the order between our read and the compare-and-set.
            throw new InvalidOperationException("The food order was just updated by someone else. Refresh and try again.");
        }

        var order = await _uow.InvoiceStore.GetPickupOrderByIdAsync(invoiceId);
        if (order == null)
        {
            throw new KeyNotFoundException("Food order not found.");
        }

        await NotifyUpdatedAsync(order);
        return ToDto(order);
    }

    public async Task<List<LowStockItemDTO>> GetLowStockAsync(Guid theaterId)
    {
        var rows = await _uow.FoodAndDrinkStore.GetLowStockAsync(theaterId);
        return rows.Select(r => new LowStockItemDTO
        {
            FoodAndDrinkId = r.FoodAndDrinkId,
            Name = r.Name,
            QuantityOnHand = r.QuantityOnHand,
            LowStockThreshold = r.LowStockThreshold,
            TargetStockLevel = r.TargetStockLevel
        }).ToList();
    }

    public async Task<PickupOrderDTO> LookupPickupAsync(Guid theaterId, string code)
    {
        var trimmed = code?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            throw new InvalidOperationException("An invoice code is required.");
        }

        var order = await _uow.InvoiceStore.GetPickupOrderByCodeAsync(theaterId, trimmed);
        if (order == null)
        {
            throw new KeyNotFoundException("No food order found for this code.");
        }
        return ToDto(order);
    }

    private static bool IsAllowedMove(FoodOrderStatus from, FoodOrderStatus to)
    {
        return (from == FoodOrderStatus.Pending && to == FoodOrderStatus.Preparing)
            || (from == FoodOrderStatus.Preparing && to == FoodOrderStatus.Ready)
            || (from == FoodOrderStatus.Ready && to == FoodOrderStatus.HandedOver);
    }

    // The status change is already committed; a failed push must not fail the staff member's request.
    private async Task NotifyUpdatedAsync(PickupOrderRow order)
    {
        try
        {
            await _notifications.NotifyFoodOrderUpdatedAsync(order.TheaterId, new FoodOrderUpdateDTO
            {
                InvoiceId = order.InvoiceId,
                TheaterId = order.TheaterId,
                InvoiceCode = order.InvoiceCode,
                FoodStatus = order.FoodStatus,
                FoodHandedOverAt = order.FoodHandedOverAt
            });
        }
        catch (Exception e)
        {
            LogProvider.Current.Warning(e, $"{nameof(ConcessionManager)}.{nameof(NotifyUpdatedAsync)} push failed for {order.InvoiceCode}: {e.Message}");
        }
    }

    internal static PickupOrderDTO ToDto(PickupOrderRow row)
    {
        return new PickupOrderDTO
        {
            InvoiceId = row.InvoiceId,
            TheaterId = row.TheaterId,
            InvoiceCode = row.InvoiceCode,
            InvoiceStatus = row.InvoiceStatus,
            Channel = row.Channel,
            FoodStatus = row.FoodStatus,
            PaidAt = row.PaidAt,
            FoodHandedOverAt = row.FoodHandedOverAt,
            CustomerName = row.CustomerName,
            ShowTimeStart = row.ShowTimeStart,
            MovieTitle = row.MovieTitle,
            RoomName = row.RoomName,
            Items = row.Items.Select(i => new PickupItemDTO { Name = i.Name, Quantity = i.Quantity }).ToList()
        };
    }
}
