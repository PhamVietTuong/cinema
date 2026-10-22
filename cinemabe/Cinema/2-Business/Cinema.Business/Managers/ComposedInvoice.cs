using Cinema.Business.DTO.Booking;
using Cinema.Business.DTO.BoxOffice;
using Cinema.Data.Entities;

namespace Cinema.Business.Managers;

internal sealed class ComposedInvoice
{
    /// <summary>Unsaved invoice (tickets + food lines attached). Status/Channel/payment fields are the caller's.</summary>
    public Invoice Invoice { get; init; } = null!;
    public List<TicketItemDTO> TicketItems { get; init; } = new();
    public List<StockDemand> StockDemand { get; init; } = new();
    public List<CounterQuoteLineDTO> Lines { get; init; } = new();
    public List<PriceOverrideLine> Overrides { get; init; } = new();
    public double PointsValue { get; init; }
    public string? PromotionTitle { get; init; }
}
