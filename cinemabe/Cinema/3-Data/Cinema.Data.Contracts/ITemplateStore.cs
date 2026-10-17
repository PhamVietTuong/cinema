using Cinema.Data.Entities;

namespace Cinema.Data.Contracts;

public interface ITemplateStore : IGenericStore<Template>
{
    /// <summary>Active templates matching any of the given codes, projected (no tracking), for rendering.</summary>
    Task<IEnumerable<TemplateContentRow>> GetActiveByCodesAsync(IReadOnlyCollection<string> codes);
}
