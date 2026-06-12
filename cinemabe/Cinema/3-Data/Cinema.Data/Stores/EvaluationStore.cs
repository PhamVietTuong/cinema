using Cinema.Data.Contexts;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;

namespace Cinema.Data.Stores;

public class EvaluationStore : GenericStore<Evaluation>, IEvaluationStore
{
    public EvaluationStore(CinemaContext db) : base(db)
    {
    }
}
