using Cinema.Data.Enums;

namespace Cinema.Data.Contracts;

/// <summary>A member profile projected for the customer card (no password/PIN columns ever leave the store).</summary>
public record CustomerRow(Guid Id, string Name, string Email, string Phone, string? MembershipName, int Points);

/// <summary>One invoice of a customer lookup, flattened (one projected query for the whole list).</summary>
public record CustomerInvoiceRow(
    Guid Id,
    string Code,
    Guid? TheaterId,
    string? TheaterName,
    InvoiceStatus Status,
    SalesChannel Channel,
    double FinalAmount,
    DateTime? PaidAt,
    DateTime CreationTime,
    string? MovieTitle,
    DateTime? FirstShowStart,
    int TicketCount);

/// <summary>What a resend needs from an invoice: its theater, state, the customer's contact and the ticket QR tokens.</summary>
public record ResendInvoiceRow(
    Guid Id,
    string Code,
    Guid? TheaterId,
    InvoiceStatus Status,
    double FinalAmount,
    string? UserEmail,
    string? UserPhone,
    List<string> QrCodes);

/// <summary>The few invoice columns a complaint needs to validate its link.</summary>
public record InvoiceHeaderRow(Guid Id, string Code, Guid? TheaterId, Guid? UserId, InvoiceStatus Status, double FinalAmount);

public interface ICustomerServiceStore
{
    /// <summary>A customer account (user type <paramref name="customerTypeName"/>) by exact email, or null.</summary>
    Task<CustomerRow?> FindCustomerByEmailAsync(string email, string customerTypeName);

    /// <summary>A customer account by exact phone, or null.</summary>
    Task<CustomerRow?> FindCustomerByPhoneAsync(string phone, string customerTypeName);

    /// <summary>The customer account that owns the invoice with this exact code, or null (unknown code or walk-in invoice).</summary>
    Task<CustomerRow?> FindCustomerByInvoiceCodeAsync(string invoiceCode, string customerTypeName);

    /// <summary>
    /// The newest invoices, untracked, limited to <paramref name="theaterIds"/> (null = all theaters). Give a
    /// <paramref name="userId"/> for a customer's invoices, or an exact <paramref name="invoiceCode"/> for one
    /// invoice (a walk-in sale). Theater names come from one batched lookup.
    /// </summary>
    Task<List<CustomerInvoiceRow>> GetInvoicesAsync(Guid? userId, string? invoiceCode, IReadOnlyCollection<Guid>? theaterIds, int take);

    Task<ResendInvoiceRow?> GetResendInvoiceAsync(Guid invoiceId);

    Task<InvoiceHeaderRow?> GetInvoiceHeaderAsync(Guid invoiceId);

    /// <summary>How many audit rows of <paramref name="action"/> exist for the entity since <paramref name="sinceUtc"/>.</summary>
    Task<int> CountRecentAuditsAsync(Guid entityId, AuditAction action, DateTime sinceUtc);
}
