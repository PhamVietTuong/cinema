using Cinema.Data.Entities;
namespace Cinema.Data.Contracts;
public interface IApplicationUnitOfWork : IDisposable
{
    IMovieStore MovieStore { get; }
    ITheaterStore TheaterStore { get; }
    IShowTimeStore ShowTimeStore { get; }
    ISeatStore SeatStore { get; }
    IInvoiceStore InvoiceStore { get; }
    IUserStore UserStore { get; }
    IRoomStore RoomStore { get; }
    IRoomTypeStore RoomTypeStore { get; }
    IDiscountStore DiscountStore { get; }
    IMovieTypeDetailStore MovieTypeDetailStore { get; }
    IUserTypeStore UserTypeStore { get; }
    ICommentStore CommentStore { get; }
    IEvaluationStore EvaluationStore { get; }
    IFoodAndDrinkStore FoodAndDrinkStore { get; }
    IAgeRestrictionStore AgeRestrictionStore { get; }
    IDiscountTypeStore DiscountTypeStore { get; }
    IMovieTypeStore MovieTypeStore { get; }
    ISeatTypeStore SeatTypeStore { get; }
    IMemberShipStore MemberShipStore { get; }
    IHolidayStore HolidayStore { get; }
    INewsStore NewsStore { get; }
    ITimeSlotStore TimeSlotStore { get; }
    ITicketPriceStore TicketPriceStore { get; }
    IReminderLogStore ReminderLogStore { get; }
    IGiftCardStore GiftCardStore { get; }
    IPatronCategoryStore PatronCategoryStore { get; }
    IPatronCategorySeatTypeStore PatronCategorySeatTypeStore { get; }
    Task<int> SaveChangesAsync();
    Task BeginTransactionAsync();
    Task CommitTransactionAsync();
    Task RollbackTransactionAsync();
}
