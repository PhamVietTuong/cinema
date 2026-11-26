using Cinema.Data.Entities;

namespace Cinema.Data.Contracts;

public interface IMemberShipStore : IGenericStore<MemberShip>
{
    /// <summary>The tier with the highest MinPoints that is still &lt;= <paramref name="points"/>, picked in SQL; null when none qualifies.</summary>
    Task<MemberShip?> GetHighestTierForPointsAsync(int points);
}
