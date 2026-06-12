using Cinema.Business.Contracts;
using Cinema.Business.Managers;
using Microsoft.Extensions.DependencyInjection;

namespace Cinema.Business;

public static class DependencyInjection
{
    public static IServiceCollection AddBusiness(this IServiceCollection services)
    {
        services.AddScoped<IAuthManager, AuthManager>();
        services.AddScoped<IMovieManager, MovieManager>();
        services.AddScoped<IBookingManager, BookingManager>();
        services.AddScoped<IInvoiceManager, InvoiceManager>();
        services.AddScoped<ITheaterManager, TheaterManager>();

        // Catalog (simple lookup) managers
        services.AddScoped<IAgeRestrictionManager, AgeRestrictionManager>();
        services.AddScoped<IDiscountTypeManager, DiscountTypeManager>();
        services.AddScoped<IMovieTypeManager, MovieTypeManager>();
        services.AddScoped<ISeatTypeManager, SeatTypeManager>();
        services.AddScoped<ITicketTypeManager, TicketTypeManager>();
        services.AddScoped<IUserTypeManager, UserTypeManager>();
        services.AddScoped<IMemberShipManager, MemberShipManager>();
        services.AddScoped<IHolidayManager, HolidayManager>();
        services.AddScoped<INewsManager, NewsManager>();
        services.AddScoped<IDiscountManager, DiscountManager>();
        services.AddScoped<IFoodAndDrinkManager, FoodAndDrinkManager>();
        services.AddScoped<IRoomManager, RoomManager>();
        services.AddScoped<IShowTimeManager, ShowTimeManager>();
        services.AddScoped<IMovieTypeDetailManager, MovieTypeDetailManager>();
        services.AddScoped<ISeatTypeTicketTypeManager, SeatTypeTicketTypeManager>();
        services.AddScoped<IInvoiceAdminManager, InvoiceAdminManager>();
        return services;
    }
}
