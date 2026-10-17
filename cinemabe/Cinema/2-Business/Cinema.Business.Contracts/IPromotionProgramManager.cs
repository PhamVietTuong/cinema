using Cinema.Business.DTO.Catalog;
using Cinema.Business.DTO.Requests;
using Cinema.Data.Entities;

namespace Cinema.Business.Contracts;

public interface IPromotionProgramManager
{
    /// <summary>Admin listing: filters "keyword" (Title) and "isPublished" (bool).</summary>
    Task<DefaultSearchResults<PromotionProgramDTO>> GetAsync(PagingSearchDTO search);
    /// <summary>Public listing for CinemaUser: published, not-yet-ended programs, optionally scoped to
    /// a theater via filter "theaterId".</summary>
    Task<DefaultSearchResults<PromotionProgramDTO>> GetPublishedAsync(PagingSearchDTO search);
    Task<bool> ExistsAsync(Guid id);
    Task<PromotionProgramDTO> GetByIdAsync(Guid id);
    Task<PromotionProgramDTO> CreateAsync(CreatePromotionProgramRequest request);
    Task<PromotionProgramDTO> UpdateAsync(UpdatePromotionProgramRequest request);
    Task DeleteAsync(Guid id);
}
