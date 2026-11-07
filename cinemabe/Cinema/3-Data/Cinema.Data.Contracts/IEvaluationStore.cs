using Cinema.Data.Entities;

namespace Cinema.Data.Contracts;

public interface IEvaluationStore : IGenericStore<Evaluation>
{
    /// <summary>The user's own (Score, Review) for a movie, or null when they have not rated it. Untracked.</summary>
    Task<(int Score, string? Review)?> GetUserEvaluationAsync(Guid movieId, Guid userId);

    /// <summary>Inserts and saves a first rating. Returns false (nothing stored) when the unique
    /// (MovieId, UserId) index rejects it because a concurrent request already created the row.</summary>
    Task<bool> TryAddAsync(Evaluation evaluation);
}
