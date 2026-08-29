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

    public IMovieStore MovieStore { get; }
    public ITheaterStore TheaterStore { get; }
    public IShowTimeStore ShowTimeStore { get; }
    public ISeatStore SeatStore { get; }
    public IInvoiceStore InvoiceStore { get; }
    public IUserStore UserStore { get; }
    public IRoomStore RoomStore { get; }
    public IRoomTypeStore RoomTypeStore { get; }
    public IDiscountStore DiscountStore { get; }
    public IMovieTypeDetailStore MovieTypeDetailStore { get; }
    public IUserTypeStore UserTypeStore { get; }
    public ICommentStore CommentStore { get; }
    public IEvaluationStore EvaluationStore { get; }
    public IFoodAndDrinkStore FoodAndDrinkStore { get; }
    public IAgeRestrictionStore AgeRestrictionStore { get; }
    public IDiscountTypeStore DiscountTypeStore { get; }
    public IMovieTypeStore MovieTypeStore { get; }
    public ISeatTypeStore SeatTypeStore { get; }
    public IMemberShipStore MemberShipStore { get; }
    public IHolidayStore HolidayStore { get; }
    public INewsStore NewsStore { get; }
    public ITimeSlotStore TimeSlotStore { get; }
    public ITicketPriceStore TicketPriceStore { get; }
    public IReminderLogStore ReminderLogStore { get; }
    public IGiftCardStore GiftCardStore { get; }

    public ApplicationUnitOfWork(CinemaContext db)
    {
        _db = db;
        MovieStore = new MovieStore(db);
        TheaterStore = new TheaterStore(db);
        ShowTimeStore = new ShowTimeStore(db);
        SeatStore = new SeatStore(db);
        InvoiceStore = new InvoiceStore(db);
        UserStore = new UserStore(db);
        RoomStore = new RoomStore(db);
        RoomTypeStore = new RoomTypeStore(db);
        DiscountStore = new DiscountStore(db);
        MovieTypeDetailStore = new MovieTypeDetailStore(db);
        UserTypeStore = new UserTypeStore(db);
        CommentStore = new CommentStore(db);
        EvaluationStore = new EvaluationStore(db);
        FoodAndDrinkStore = new FoodAndDrinkStore(db);
        AgeRestrictionStore = new AgeRestrictionStore(db);
        DiscountTypeStore = new DiscountTypeStore(db);
        MovieTypeStore = new MovieTypeStore(db);
        SeatTypeStore = new SeatTypeStore(db);
        MemberShipStore = new MemberShipStore(db);
        HolidayStore = new HolidayStore(db);
        NewsStore = new NewsStore(db);
        TimeSlotStore = new TimeSlotStore(db);
        TicketPriceStore = new TicketPriceStore(db);
        ReminderLogStore = new ReminderLogStore(db);
        GiftCardStore = new GiftCardStore(db);
    }

    public Task<int> SaveChangesAsync()
    {
        return _db.SaveChangesAsync();
    }

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
