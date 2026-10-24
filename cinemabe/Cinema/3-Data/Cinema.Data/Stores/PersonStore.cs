using Cinema.Data.Contexts;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Cinema.Data.Stores;

public class PersonStore : GenericStore<Person>, IPersonStore
{
    public PersonStore(CinemaContext db) : base(db)
    {
    }

    public async Task<List<Person>> GetWithPhotoByNamesAsync(IReadOnlyCollection<string> names)
    {
        if (names.Count == 0)
        {
            return new List<Person>();
        }
        return await DbSet
            .AsNoTracking()
            .Where(p => names.Contains(p.Name) && p.PhotoUrl != null && p.PhotoUrl != "")
            .ToListAsync();
    }

    public void TrackNew(Person person)
    {
        Context.Add(person);
    }
}
