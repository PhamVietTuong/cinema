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

    /// <summary>Creates one independent pricing row.</summary>
    Task<PatronCategoryDTO>                       CreateAsync(CreatePatronCategoryRequest request);

    /// <summary>Updates exactly the row with this Id — never touches rows sharing its Name.</summary>
    Task<PatronCategoryDTO>                       UpdateAsync(UpdatePatronCategoryRequest request);

    /// <summary>Deletes exactly this row.</summary>
    Task                                          DeleteAsync(Guid id);
}
