using Cinema.Data.Entities;
using Cinema.Data.Enums;

namespace Cinema.Data.Contracts;

public interface ICashDrawerStore : IGenericStore<CashDrawerSession>
{
    /// <summary>The user's Open drawer session (tracked), or null.</summary>
    Task<CashDrawerSession?> GetOpenForUserAsync(Guid userId);

    /// <summary>Whether another Open session already uses this terminal name in the theater.</summary>
    Task<bool> IsTerminalInUseAsync(Guid theaterId, string terminalName);

    /// <summary>Tracks the session WITHOUT saving; persisted by the caller's SaveChanges.</summary>
    void StageSession(CashDrawerSession session);

    /// <summary>Tracks the movement WITHOUT saving, so it commits with the caller's own transaction/SaveChanges.</summary>
    void StageMovement(CashMovement movement);

    /// <summary>Sum of movement amounts per type for a session (one grouped query).</summary>
    Task<Dictionary<CashMovementType, double>> GetTotalsByTypeAsync(Guid sessionId);

    /// <summary>Newest movements of a session, untracked.</summary>
    Task<List<CashMovement>> GetRecentMovementsAsync(Guid sessionId, int take);
}
