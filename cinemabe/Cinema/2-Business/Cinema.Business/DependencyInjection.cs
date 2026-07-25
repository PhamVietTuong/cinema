using Cinema.Business.Contracts;
using Cinema.Business.Managers;
using Cinema.Business.Notifications;
using Microsoft.Extensions.DependencyInjection;

namespace Cinema.Business;

public static class DependencyInjection
{
    public static IServiceCollection AddBusiness(this IServiceCollection services)
    {
        services.AddScoped<IAuthManager, AuthManager>();
        services.AddSingleton<INotificationService, DevLogNotificationService>();
        services.AddSingleton<ISmsNotificationService, DevLogSmsNotificationService>();
        services.AddSingleton<IGoogleTokenValidator, GoogleTokenValidator>();
        services.AddSingleton<IFacebookTokenValidator, FacebookTokenValidator>();
        services.AddScoped<IMovieManager, MovieManager>();
        services.AddScoped<IBookingManager, BookingManager>();
        // Payment gateways (Sandbox + VNPay/MoMo/Stripe) and their resolver are registered in the
        // Web API host's Program.cs, where IConfiguration is available to read the "Payments" section.
        services.AddScoped<IInvoiceManager, InvoiceManager>();
        services.AddScoped<IGiftCardManager, GiftCardManager>();
        services.AddScoped<ITheaterManager, TheaterManager>();

        // Catalog (simple lookup) managers
        services.AddScoped<IAgeRestrictionManager, AgeRestrictionManager>();
        services.AddScoped<IDiscountTypeManager, DiscountTypeManager>();
        services.AddScoped<IMovieTypeManager, MovieTypeManager>();
        services.AddScoped<ISeatTypeManager, SeatTypeManager>();
        services.AddScoped<IUserTypeManager, UserTypeManager>();
        services.AddScoped<IMemberShipManager, MemberShipManager>();
        services.AddScoped<IHolidayManager, HolidayManager>();
        services.AddScoped<INewsManager, NewsManager>();
        services.AddScoped<IDiscountManager, DiscountManager>();
        services.AddScoped<IFoodAndDrinkManager, FoodAndDrinkManager>();
        services.AddScoped<IRoomManager, RoomManager>();
        services.AddScoped<IShowTimeManager, ShowTimeManager>();
        services.AddScoped<IMovieTypeDetailManager, MovieTypeDetailManager>();
        services.AddScoped<IInvoiceAdminManager, InvoiceAdminManager>();
        services.AddScoped<IRoomTypeManager, RoomTypeManager>();
        services.AddScoped<ITimeSlotManager, TimeSlotManager>();
        services.AddScoped<ITicketPriceManager, TicketPriceManager>();
        return services;
    }
}
