using Cinema.Business.DTO.Catalog;

namespace Cinema.Business.Contracts;

public interface IComboManager
{
    /// <summary>The recipe of a combo (empty when it is not a combo). Public read.</summary>
    Task<List<ComboComponentDTO>> GetComponentsAsync(Guid comboId);

    /// <summary>Turns an item into a combo with the given recipe, or back into a plain item.</summary>
    Task SaveAsync(SaveComboRequest request);
}
