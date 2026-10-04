using Cinema.Business.DTO.BoxOffice;

namespace Cinema.Business.Contracts;

/// <summary>
/// Staff point of sale: counter ticket and food sales with split tenders, plus the cashier's cash drawer.
/// Every method takes the already-resolved theater (the controller resolves it from the caller's scope).
/// Authorization failures throw <c>AccessDeniedException</c> (403), never <c>UnauthorizedAccessException</c> (401).
/// </summary>
public interface IBoxOfficeManager
{
    /// <summary>Prices a counter sale exactly like <see cref="SellAsync"/> but has no side effects
    /// (no stock, points, gift-card or seat change, no invoice).</summary>
    Task<CounterQuoteDTO> QuoteAsync(Guid theaterId, CounterSaleRequest request);

    /// <summary>Rings up a counter sale: always creates a Paid invoice (never Pending) in one transaction.</summary>
    Task<CounterSaleResultDTO> SellAsync(Guid theaterId, Guid staffUserId, CounterSaleRequest request);

    Task<CashDrawerDTO> OpenDrawerAsync(Guid theaterId, Guid staffUserId, OpenDrawerRequest request);

    /// <summary>The caller's open drawer with totals, or <c>IsOpen = false</c>.</summary>
    Task<CashDrawerDTO> GetMyDrawerAsync(Guid theaterId, Guid staffUserId);

    Task<CashDrawerDTO> PayInOutAsync(Guid theaterId, Guid staffUserId, PayInOutRequest request);

    Task<List<CounterShowtimeDTO>> GetShowtimesTodayAsync(Guid theaterId, DateTime? date);

    /// <summary>Finds a member by exact phone number so a counter sale can attach them. Throws KeyNotFound if none.</summary>
    Task<CounterCustomerDTO> FindCustomerAsync(string phone);
}
