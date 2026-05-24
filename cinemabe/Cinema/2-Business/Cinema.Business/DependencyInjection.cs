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
        return services;
    }
}
