using Cinema.Data.Entities;

namespace Cinema.Data.Contracts;

public interface IUserStore : IGenericStore<User>
{
    Task<User?> GetByEmailAsync(string email);
    Task<User?> GetByPhoneAsync(string phone);
    Task<(IEnumerable<User> Items, int Total)> GetPagedAsync(string? search, int page, int pageSize);

    /// <summary>Display name by user id for a batch of ids (one query). Unknown ids are absent.</summary>
    Task<Dictionary<Guid, string>> GetNamesByIdsAsync(IReadOnlyCollection<Guid> ids);

    /// <summary>
    /// Active users who could approve an override for a theater, as (Id, Name), ordered by name, one projected query:
    /// users of <paramref name="theaterRoleNames"/> assigned to <paramref name="theaterId"/>, plus users of
    /// <paramref name="globalRoleNames"/> (who are not bound to a theater).
    /// </summary>
    Task<List<(Guid Id, string Name)>> GetApproversAsync(
        Guid theaterId,
        IReadOnlyCollection<string> theaterRoleNames,
        IReadOnlyCollection<string> globalRoleNames,
        IReadOnlyCollection<string> assignedRoleNames);

    /// <summary>The theaters a user (RegionalManager) is assigned to, untracked (one query).</summary>
    Task<List<Guid>> GetAssignedTheaterIdsAsync(Guid userId);

    /// <summary>Stages the user's assignment set to exactly <paramref name="theaterIds"/> WITHOUT saving (one read).</summary>
    Task ReplaceAssignedTheatersAsync(Guid userId, IReadOnlyCollection<Guid> theaterIds);
}
