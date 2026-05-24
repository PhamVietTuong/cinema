using Cinema.Business.Contracts;
using Cinema.Data.Contracts;
using Cinema.Data.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Cinema.Data.Contexts;

namespace Cinema.Data;

public static class DependencyInjection
{
    public static IServiceCollection AddData(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<CinemaContext>(options =>
            options.UseSqlServer(config.GetConnectionString("CinemaDatabase")));

        services.AddScoped<IApplicationUnitOfWork, ApplicationUnitOfWork>();
        services.AddScoped<ITokenService, JwtTokenService>();
        return services;
    }
}
