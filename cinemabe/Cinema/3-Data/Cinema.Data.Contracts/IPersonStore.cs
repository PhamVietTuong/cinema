using Cinema.Data.Entities;

namespace Cinema.Data.Contracts;

public interface IPersonStore : IGenericStore<Person>
{
    Task<List<Person>> GetWithPhotoByNamesAsync(IReadOnlyCollection<string> names);
}
