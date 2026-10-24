using Cinema.Data.Entities;

namespace Cinema.Data.Contracts;

public interface IMovieCreditStore : IGenericStore<MovieCredit>
{
    Task<IReadOnlyList<MovieCreditRow>> GetByMovieAsync(Guid movieId);

    /// <summary>Explicitly marks a new MovieCredit as Added, without saving. Needed because appending
    /// it to an already-tracked Movie's MovieCredits collection is not enough: BaseEntity assigns Id
    /// a non-default Guid in its property initializer, so EF Core's change-tracker fixup treats the
    /// new row as pre-existing (Modified) instead of new (Added), producing a no-op UPDATE that
    /// throws DbUpdateConcurrencyException at SaveChanges. Call this instead of
    /// `movie.MovieCredits.Add(...)` for brand-new rows during a reconcile against a tracked Movie.</summary>
    void TrackNew(MovieCredit credit);
}
