using Cinema.Business.DTO.Catalog;
using Cinema.Business.DTO.Requests;
using Cinema.Data.Entities;

namespace Cinema.Business.Contracts;

public interface ISplashBannerManager
{
    /// <summary>Admin listing: filters "keyword" (Title) and "isPublished" (bool).</summary>
    Task<DefaultSearchResults<SplashBannerDTO>> GetAsync(PagingSearchDTO search);
    /// <summary>Public lookup for CinemaUser: the single active banner (published, in its date
    /// window) if any, as a 0- or 1-item list — never null, so the response never becomes a 204.</summary>
    Task<List<SplashBannerDTO>> GetActiveAsync();
    Task<bool> ExistsAsync(Guid id);
    Task<SplashBannerDTO> GetByIdAsync(Guid id);
    Task<SplashBannerDTO> CreateAsync(CreateSplashBannerRequest request);
    Task<SplashBannerDTO> UpdateAsync(UpdateSplashBannerRequest request);
    Task DeleteAsync(Guid id);
}
