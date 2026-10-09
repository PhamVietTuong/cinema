using Cinema.Data.Contexts;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Cinema.Data.Enums;
using Microsoft.EntityFrameworkCore;

namespace Cinema.Data.Stores;

public class CashDrawerStore : GenericStore<CashDrawerSession>, ICashDrawerStore
{
    public CashDrawerStore(CinemaContext db) : base(db)
    {
    }

    public async Task<CashDrawerSession?> GetOpenForUserAsync(Guid userId)
    {
        return await DbSet.FirstOrDefaultAsync(s => s.UserId == userId && s.Status == CashDrawerStatus.Open);
    }

    public async Task<bool> IsTerminalInUseAsync(Guid theaterId, string terminalName)
    {
        return await DbSet.AnyAsync(s => s.TheaterId == theaterId && s.TerminalName == terminalName && s.Status == CashDrawerStatus.Open);
    }

    public void StageSession(CashDrawerSession session)
    {
        DbSet.Add(session);
    }

    public void StageMovement(CashMovement movement)
    {
        Context.CashMovement.Add(movement);
    }

    public async Task<Dictionary<CashMovementType, double>> GetTotalsByTypeAsync(Guid sessionId)
    {
        return await Context.CashMovement
            .AsNoTracking()
            .Where(m => m.CashDrawerSessionId == sessionId)
            .GroupBy(m => m.Type)
            .Select(g => new { Type = g.Key, Total = g.Sum(m => m.Amount) })
            .ToDictionaryAsync(x => x.Type, x => x.Total);
    }

    public async Task<List<CashMovement>> GetRecentMovementsAsync(Guid sessionId, int take)
    {
        return await Context.CashMovement
            .AsNoTracking()
            .Where(m => m.CashDrawerSessionId == sessionId)
            .OrderByDescending(m => m.CreationTime)
            .ThenByDescending(m => m.Id)
            .Take(take)
            .ToListAsync();
    }
}
