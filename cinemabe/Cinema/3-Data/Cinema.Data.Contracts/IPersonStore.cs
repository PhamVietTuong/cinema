using Cinema.Data.Entities;

namespace Cinema.Data.Contracts;

public interface IPersonStore : IGenericStore<Person>
{
    Task<List<Person>> GetWithPhotoByNamesAsync(IReadOnlyCollection<string> names);

    /// <summary>Id of each existing Person whose Name matches one of `names` (DB collation, case-
    /// insensitive), keyed by name with an OrdinalIgnoreCase comparer so callers can look up by the
    /// exact string they searched with.</summary>
    Task<Dictionary<string, Guid>> GetIdsByNamesAsync(IReadOnlyCollection<string> names);

    /// <summary>Stages a new Person as Added without saving, so its creation commits atomically with
    /// whatever SaveChanges the caller performs next (e.g. alongside a MovieCredit referencing it).
    /// Mirrors MovieCreditStore.TrackNew for the same reason: GenericStore.CreateAsync saves
    /// immediately, which would commit the Person even if the rest of the caller's work later fails.</summary>
    void TrackNew(Person person);
}
