using Cinema.Business.DTO.BoxOffice;
using Cinema.Data.Entities;

namespace Cinema.Business.Managers;

internal sealed record FoodBuild(
    List<InvoiceFoodAndDrink> Lines,
    double Total,
    List<StockDemand> Demand,
    List<CounterQuoteLineDTO> QuoteLines,
    List<PriceOverrideLine> Overrides);
