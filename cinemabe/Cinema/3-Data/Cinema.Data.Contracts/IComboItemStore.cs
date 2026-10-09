using Cinema.Data.Entities;

namespace Cinema.Data.Contracts;

public interface IComboItemStore : IGenericStore<ComboItem>
{
    /// <summary>All component rows for the given combos in one read-only query.</summary>
    Task<List<ComboItem>> GetByCombosAsync(IReadOnlyCollection<Guid> comboIds);

    /// <summary>The combos (id + name) that currently use <paramref name="componentId"/> as a component.</summary>
    Task<List<(Guid ComboId, string ComboName)>> GetCombosUsingAsync(Guid componentId);

    /// <summary>Replaces a combo's whole component list. The caller owns the surrounding transaction.</summary>
    Task ReplaceForComboAsync(Guid comboId, IReadOnlyList<ComboItem> items);
}
