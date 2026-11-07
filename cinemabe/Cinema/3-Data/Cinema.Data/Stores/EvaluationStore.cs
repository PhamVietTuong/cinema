using Cinema.Data.Contexts;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Cinema.Data.Stores;

public class EvaluationStore : GenericStore<Evaluation>, IEvaluationStore
{
    public EvaluationStore(CinemaContext db) : base(db)
    {
    }

    public async Task<bool> TryAddAsync(Evaluation evaluation)
    {
        DbSet.Add(evaluation);
        try
        {
            await Context.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateException e) when (e.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 })
        {
            Context.Entry(evaluation).State = EntityState.Detached;
            return false;
        }
    }

    public async Task<(int Score, string? Review)?> GetUserEvaluationAsync(Guid movieId, Guid userId)
    {
        var row = await DbSet
            .AsNoTracking()
            .Where(e => e.MovieId == movieId && e.UserId == userId)
            .Select(e => new { e.Score, e.Review })
            .FirstOrDefaultAsync();
        return row == null ? null : (row.Score, row.Review);
    }
}
