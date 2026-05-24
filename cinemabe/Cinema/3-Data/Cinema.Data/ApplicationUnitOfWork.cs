using Cinema.Data.Contexts;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Cinema.Data.Stores;
using Microsoft.EntityFrameworkCore.Storage;

namespace Cinema.Data;

public class ApplicationUnitOfWork : IApplicationUnitOfWork
{
    private readonly CinemaContext _db;
    private IDbContextTransaction? _transaction;

    public IMovieStore Movies { get; }
    public ITheaterStore Theaters { get; }
    public IShowTimeStore ShowTimes { get; }
    public ISeatStore Seats { get; }
    public IInvoiceStore Invoices { get; }
    public IUserStore Users { get; }
    public IGenericStore<UserType> UserTypes { get; }
    public IGenericStore<Comment> Comments { get; }
    public IGenericStore<Evaluation> Evaluations { get; }
    public IGenericStore<FoodAndDrink> FoodAndDrinks { get; }

    public ApplicationUnitOfWork(CinemaContext db)
    {
        _db = db;
        Movies = new MovieStore(db);
        Theaters = new TheaterStore(db);
        ShowTimes = new ShowTimeStore(db);
        Seats = new SeatStore(db);
        Invoices = new InvoiceStore(db);
        Users = new UserStore(db);
        UserTypes = new GenericStore<UserType>(db);
        Comments = new GenericStore<Comment>(db);
        Evaluations = new GenericStore<Evaluation>(db);
        FoodAndDrinks = new GenericStore<FoodAndDrink>(db);
    }

    public Task<int> SaveChangesAsync() => _db.SaveChangesAsync();

    public async Task BeginTransactionAsync()
        => _transaction = await _db.Database.BeginTransactionAsync();

    public async Task CommitTransactionAsync()
    {
        if (_transaction != null) { await _transaction.CommitAsync(); await _transaction.DisposeAsync(); _transaction = null; }
    }

    public async Task RollbackTransactionAsync()
    {
        if (_transaction != null) { await _transaction.RollbackAsync(); await _transaction.DisposeAsync(); _transaction = null; }
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        _db.Dispose();
    }
}
