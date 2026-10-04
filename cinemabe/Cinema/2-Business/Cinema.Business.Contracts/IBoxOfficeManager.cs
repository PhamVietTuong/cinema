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

    // ── After-sales (P5) ────────────────────────────────────────────────────────

    /// <summary>Finds invoices of the theater by exact code and/or customer phone (at least one is required).</summary>
    Task<List<AfterSalesInvoiceDTO>> FindInvoiceAsync(Guid theaterId, FindInvoiceRequest request);

    /// <summary>
    /// Refunds a whole Paid invoice. A manager approval is required unless the actor is a manager (otherwise
    /// <c>AccessDeniedException</c>, 403). Frees the seats, restores stock via the ledger, gives back gift card,
    /// points and promo usage, writes a negative cash movement for a cash refund and an audit row with actor and
    /// approver. Also usable by customer-service compensation flows (P7) since it takes only ids and a request.
    /// </summary>
    Task<StaffRefundResultDTO> StaffRefundAsync(Guid theaterId, Guid staffUserId, StaffRefundRequest request);

    /// <summary>Replaces a counter invoice by a new sale in ONE transaction; the tenders settle only the price difference.</summary>
    Task<ExchangeResultDTO> ExchangeAsync(Guid theaterId, Guid staffUserId, ExchangeRequest request);

    /// <summary>Returns the tickets of a Paid invoice again. Audited; needs a manager approval only if a ticket was used.</summary>
    Task<ReprintResultDTO> ReprintAsync(Guid theaterId, Guid staffUserId, ReprintRequest request);

    /// <summary>Closes the caller's own open drawer: expected = sum of its movements, variance = counted - expected.
    /// Beyond <c>CashDrawer:VarianceTolerance</c> the session stays Closed until <see cref="ReconcileDrawerAsync"/>.</summary>
    Task<CloseDrawerResultDTO> CloseDrawerAsync(Guid theaterId, Guid staffUserId, CloseDrawerRequest request);

    /// <summary>A manager accepts the variance of a Closed session (audited).</summary>
    Task<CloseDrawerResultDTO> ReconcileDrawerAsync(Guid theaterId, Guid staffUserId, ReconcileDrawerRequest request);
}
