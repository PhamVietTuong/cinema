using Cinema.Business.DTO.Concession;
using Cinema.Data.Enums;

namespace Cinema.Business.Contracts;

/// <summary>
/// The concession counter's pickup queue and low-stock view. The <c>theaterId</c> arguments are already resolved
/// against the caller's scope by the controller.
/// </summary>
public interface IConcessionManager
{
    /// <summary>
    /// Paid invoices of the theater whose food is Pending, Preparing or Ready, ordered by earliest showtime start
    /// (food-only sales after them, oldest first). <paramref name="day"/> null = today (local time).
    /// </summary>
    Task<List<PickupOrderDTO>> GetPickupQueueAsync(Guid theaterId, DateTime? day);

    /// <summary>
    /// Moves a paid order one step along Pending, Preparing, Ready, HandedOver. HandedOver records the staff member
    /// and the time. An illegal move (or an unpaid invoice) throws <see cref="InvalidOperationException"/> (400); an
    /// unknown invoice or another theater's invoice throws <see cref="KeyNotFoundException"/> (404).
    /// </summary>
    Task<PickupOrderDTO> SetFoodStatusAsync(Guid theaterId, Guid staffId, Guid invoiceId, FoodOrderStatus next);

    /// <summary>Tracked items with <c>QuantityOnHand &lt;= LowStockThreshold</c>, emptiest first.</summary>
    Task<List<LowStockItemDTO>> GetLowStockAsync(Guid theaterId);

    /// <summary>Finds an order by the invoice code the customer shows, whatever its pickup status. Unknown or other
    /// theater's code: <see cref="KeyNotFoundException"/> (404).</summary>
    Task<PickupOrderDTO> LookupPickupAsync(Guid theaterId, string code);
}
