using Cinema.Data.Entities;
namespace Cinema.Data.Contracts;
public interface IApplicationUnitOfWork : IDisposable
{
    IMovieStore Movies { get; }
    ITheaterStore Theaters { get; }
    IShowTimeStore ShowTimes { get; }
    ISeatStore Seats { get; }
    IInvoiceStore Invoices { get; }
    IUserStore Users { get; }
    IGenericStore<UserType> UserTypes { get; }
    IGenericStore<Comment> Comments { get; }
    IGenericStore<Evaluation> Evaluations { get; }
    IGenericStore<FoodAndDrink> FoodAndDrinks { get; }
    Task<int> SaveChangesAsync();
    Task BeginTransactionAsync();
    Task CommitTransactionAsync();
    Task RollbackTransactionAsync();
}
