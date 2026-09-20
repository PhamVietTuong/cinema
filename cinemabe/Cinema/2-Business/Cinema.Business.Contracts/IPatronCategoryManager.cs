using Cinema.Business.DTO.Catalog;
using Cinema.Business.DTO.Requests;
using Cinema.Data.Entities;

namespace Cinema.Business.Contracts;

public interface IPatronCategoryManager
{
    Task<DefaultSearchResults<PatronCategoryDTO>> GetAsync(PagingSearchDTO search);
    Task<bool>                                    ExistsAsync(Guid id);
    Task<PatronCategoryDTO>                       GetByIdAsync(Guid id);

    /// <summary>All PatronCategory rows (all seat kinds) belonging to a theater — used by the
    /// RoomType price-override editor to list the categories it can override.</summary>
    Task<List<PatronCategoryDTO>>                 GetByTheaterAsync(Guid theaterId);

    /// <summary>Creates a whole logical category (one row per Prices entry) transactionally.</summary>
    Task<List<PatronCategoryDTO>>                 CreateAsync(CreatePatronCategoryRequest request);

    /// <summary>Replaces every row of a logical category (identified by any one of its sibling ids) to
    /// match the given Prices list — renaming, adding, or removing seat kinds as needed.</summary>
    Task<List<PatronCategoryDTO>>                 UpdateAsync(UpdatePatronCategoryRequest request);

    /// <summary>Deletes every row sharing the logical category (all its seat kinds).</summary>
    Task                                          DeleteAsync(Guid id);
}
