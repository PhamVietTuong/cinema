using Cinema.Data.Contexts;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Cinema.Data.Stores;

public class TemplateStore : GenericStore<Template>, ITemplateStore
{
    public TemplateStore(CinemaContext db) : base(db)
    {
    }

    public async Task<IEnumerable<TemplateContentRow>> GetActiveByCodesAsync(IReadOnlyCollection<string> codes)
    {
        return await Context.Template
            .AsNoTracking()
            .Where(t => t.IsActive && codes.Contains(t.Code))
            .Select(t => new TemplateContentRow
            {
                Code = t.Code,
                Channel = t.Channel,
                Subject = t.Subject,
                Body = t.Body
            })
            .ToListAsync();
    }
}
