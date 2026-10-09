using Cinema.Data.Entities;
using Cinema.Data.Enums;

namespace Cinema.Data.Contracts;

/// <summary>Filter + page for complaints (newest first). PageIndex is 0-based. TheaterIds null = all theaters.</summary>
public record ComplaintSearchCriteria(
    IReadOnlyCollection<Guid>? TheaterIds,
    ComplaintStatus? Status,
    ComplaintCategory? Category,
    Guid? AssignedToUserId,
    Guid? CustomerUserId,
    Guid? InvoiceId,
    int PageIndex,
    int PageSize);

/// <summary>A complaint plus the customer name and invoice code it points at (left-joined, so both may be null).</summary>
public record ComplaintRow(Complaint Complaint, string? CustomerName, string? InvoiceCode);

public interface IComplaintStore : IGenericStore<Complaint>
{
    /// <summary>Filtered complaint page with customer/invoice labels, newest first, untracked. One page query + one count.</summary>
    Task<(List<ComplaintRow> Items, int Total)> SearchAsync(ComplaintSearchCriteria criteria);

    /// <summary>One complaint with its labels, untracked, or null.</summary>
    Task<ComplaintRow?> GetRowAsync(Guid id);
}
