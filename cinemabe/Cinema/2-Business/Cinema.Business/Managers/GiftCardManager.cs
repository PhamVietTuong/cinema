using Cinema.Business.Contracts;
using Cinema.Business.DTO;
using Cinema.Business.DTO.Invoices;
using Cinema.Business.DTO.Requests;
using Cinema.Business.Extensions;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;

namespace Cinema.Business.Managers;

public class GiftCardManager : IGiftCardManager
{
    private readonly IApplicationUnitOfWork _uow;

    public GiftCardManager(IApplicationUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<DefaultSearchResults<GiftCardDTO>> GetGiftCardsAsync(PagingSearchDTO search)
    {
        var page    = search.PageIndex > 0 ? search.PageIndex : 1;
        var size    = search.PageSize  > 0 ? search.PageSize  : 20;
        var keyword = search.Filters.GetString("keyword");

        var all = await _uow.GiftCardStore.GetAllAsync();
        var filtered = all.AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            filtered = filtered.Where(g => g.Code.Contains(k) || (g.IssuedToEmail != null && g.IssuedToEmail.Contains(k)));
        }

        var ordered = filtered.OrderByDescending(g => g.CreationTime).ToList();
        return new DefaultSearchResults<GiftCardDTO>
        {
            Results      = ordered.Skip((page - 1) * size).Take(size).Select(ToDTO).ToList(),
            TotalCount   = ordered.Count,
            Page         = page,
            CountPerPage = size,
        };
    }

    public async Task<GiftCardDTO> IssueAsync(IssueGiftCardRequest request)
    {
        if (request.Amount <= 0)
        {
            throw new InvalidOperationException("Gift card amount must be positive.");
        }

        var card = new GiftCard
        {
            Code           = GenerateCode(),
            InitialBalance = request.Amount,
            Balance        = request.Amount,
            IsActive       = true,
            ExpiresAt      = request.ExpiresAt,
            IssuedToEmail  = request.IssuedToEmail,
        };
        await _uow.GiftCardStore.CreateAsync(card);
        return ToDTO(card);
    }

    public async Task<bool> SetActiveAsync(Guid id, bool active)
    {
        var card = await _uow.GiftCardStore.GetByIdAsync(id);
        if (card is null)
        {
            return false;
        }
        card.IsActive = active;
        await _uow.GiftCardStore.UpdateAsync(card);
        return true;
    }

    public async Task<GiftCardValidationDTO> ValidateAsync(string code)
    {
        var card = await _uow.GiftCardStore.GetByCodeAsync((code ?? string.Empty).Trim());
        if (card is null || !card.IsActive)
        {
            return new GiftCardValidationDTO { Valid = false, Message = "Gift card not found." };
        }
        if (card.ExpiresAt is DateTime exp && exp <= DateTime.UtcNow)
        {
            return new GiftCardValidationDTO { Valid = false, Message = "Gift card has expired." };
        }
        if (card.Balance <= 0)
        {
            return new GiftCardValidationDTO { Valid = false, Message = "Gift card has no remaining balance." };
        }
        return new GiftCardValidationDTO { Valid = true, Balance = card.Balance };
    }

    private static GiftCardDTO ToDTO(GiftCard g)
    {
        return new()
        {
            Id             = g.Id,
            Code           = g.Code,
            InitialBalance = g.InitialBalance,
            Balance        = g.Balance,
            IsActive       = g.IsActive,
            ExpiresAt      = g.ExpiresAt,
            IssuedToEmail  = g.IssuedToEmail,
            CreationTime   = g.CreationTime,
        };
    }

    private static string GenerateCode()
    {
        return "GC-" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
    }
}
