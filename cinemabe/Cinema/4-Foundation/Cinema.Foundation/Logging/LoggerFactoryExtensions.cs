using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Cinema.Foundation.Logging;

/// <summary>
/// DI registration extensions for <see cref="ILog"/>.
/// </summary>
public static class LoggerFactoryExtensions
{
    /// <summary>
    /// Registers <see cref="ILog"/> as a singleton <see cref="MainLogger"/> in the DI container.
    /// Call <see cref="FoundationLoggerFactory.SetupLogger(Microsoft.Extensions.Configuration.IConfiguration)"/>
    /// before this so that Serilog's static <see cref="Log.Logger"/> is already configured.
    /// </summary>
    public static IServiceCollection AddFoundationLogging(this IServiceCollection services)
    {
        services.AddSingleton<ILog, MainLogger>();
        return services;
    }
}
