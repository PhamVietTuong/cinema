using Cinema.Business.Contracts;
using Cinema.Business.Contracts.Payments;
using Cinema.Business.Managers;
using Cinema.Business.Notifications;
using Cinema.Business.Payments;
using Microsoft.Extensions.DependencyInjection;

namespace Cinema.Business;

public static class DependencyInjection
{
    public static IServiceCollection AddBusiness(this IServiceCollection services)
    {
        services.AddScoped<IAuthManager, AuthManager>();
        services.AddSingleton<INotificationService, DevLogNotificationService>();
        services.AddSingleton<IGoogleTokenValidator, GoogleTokenValidator>();
        services.AddScoped<IMovieManager, MovieManager>();
        services.AddScoped<IBookingManager, BookingManager>();
        // Payment provider — swap SandboxPaymentGateway for a real provider (VNPay/MoMo/Stripe).
        services.AddSingleton<IPaymentGateway, SandboxPaymentGateway>();
        services.AddScoped<IInvoiceManager, InvoiceManager>();
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
        return services;
    }
}
