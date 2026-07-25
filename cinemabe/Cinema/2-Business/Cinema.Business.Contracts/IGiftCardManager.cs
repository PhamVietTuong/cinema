using Cinema.Business.DTO.Invoices;
using Cinema.Business.DTO.Requests;
using Cinema.Data.Entities;

namespace Cinema.Business.Contracts;

public interface IGiftCardManager
{
    /// <summary>Admin: paged gift cards. Filter: "keyword" (matches code or recipient email).</summary>
    Task<DefaultSearchResults<GiftCardDTO>> GetGiftCardsAsync(PagingSearchDTO search);
    /// <summary>Admin: issue a new gift card with a generated code.</summary>
    Task<GiftCardDTO> IssueAsync(IssueGiftCardRequest request);
    /// <summary>Admin: enable/disable a gift card. Returns false if not found.</summary>
    Task<bool> SetActiveAsync(Guid id, bool active);
    /// <summary>Customer: check a gift-card code and its remaining balance before applying it.</summary>
    Task<GiftCardValidationDTO> ValidateAsync(string code);
}
