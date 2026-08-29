using Cinema.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Cinema.Data.Contexts;

public class CinemaContext : DbContext
{
    public CinemaContext(DbContextOptions<CinemaContext> options) : base(options) { }

    public DbSet<User> User => Set<User>();
    public DbSet<UserType> UserType => Set<UserType>();
    public DbSet<MemberShip> MemberShip => Set<MemberShip>();
    public DbSet<Movie> Movie => Set<Movie>();
    public DbSet<MovieType> MovieType => Set<MovieType>();
    public DbSet<MovieTypeDetail> MovieTypeDetail => Set<MovieTypeDetail>();
    public DbSet<AgeRestriction> AgeRestriction => Set<AgeRestriction>();
    public DbSet<Theater> Theater => Set<Theater>();
    public DbSet<Room> Room => Set<Room>();
    public DbSet<RoomType> RoomType => Set<RoomType>();
    public DbSet<Seat> Seat => Set<Seat>();
    public DbSet<SeatType> SeatType => Set<SeatType>();
    public DbSet<ShowTime> ShowTime => Set<ShowTime>();
    public DbSet<ShowTimeRoom> ShowTimeRoom => Set<ShowTimeRoom>();
    public DbSet<Invoice> Invoice => Set<Invoice>();
    public DbSet<InvoiceTicket> InvoiceTicket => Set<InvoiceTicket>();
    public DbSet<InvoiceFoodAndDrink> InvoiceFoodAndDrink => Set<InvoiceFoodAndDrink>();
    public DbSet<FoodAndDrink> FoodAndDrink => Set<FoodAndDrink>();
    public DbSet<Discount> Discount => Set<Discount>();
    public DbSet<DiscountTheater> DiscountTheater => Set<DiscountTheater>();
    public DbSet<DiscountType> DiscountType => Set<DiscountType>();
    public DbSet<Holiday> Holiday => Set<Holiday>();
    public DbSet<TimeSlot> TimeSlot => Set<TimeSlot>();
    public DbSet<TicketPrice> TicketPrice => Set<TicketPrice>();
    public DbSet<News> News => Set<News>();
    public DbSet<Comment> Comment => Set<Comment>();
    public DbSet<Evaluation> Evaluation => Set<Evaluation>();
    public DbSet<ReminderLog> ReminderLog => Set<ReminderLog>();
    public DbSet<GiftCard> GiftCard => Set<GiftCard>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        base.OnModelCreating(mb);
        mb.ApplyConfigurationsFromAssembly(typeof(CinemaContext).Assembly);

        // Map all DateTime / DateTime? properties to SQL datetime (not datetime2)
        foreach (var entity in mb.Model.GetEntityTypes())
        {
            foreach (var prop in entity.GetProperties())
            {
                if (prop.ClrType == typeof(DateTime) || prop.ClrType == typeof(DateTime?))
                {
                    prop.SetColumnType("datetime");
                }
            }
        }
    }
}
