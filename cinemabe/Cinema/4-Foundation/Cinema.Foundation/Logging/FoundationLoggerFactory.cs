using Microsoft.Extensions.Configuration;
using Serilog;

namespace Cinema.Foundation.Logging;

/// <summary>
/// Factory for configuring Serilog and wiring it into <see cref="LogProvider"/>.
/// </summary>
public static class FoundationLoggerFactory
{
    /// <summary>
    /// Configures Serilog from <paramref name="configuration"/> (reads Serilog section),
    /// creates a <see cref="MainLogger"/> and registers it as the application-wide default.
    /// </summary>
    public static ILog SetupLogger(IConfiguration configuration)
    {
        Log.Logger = new LoggerConfiguration()
            .Enrich.WithThreadId()
            .Enrich.WithProperty("MachineName", Environment.MachineName)
            .Enrich.FromLogContext()
            .ReadFrom.Configuration(configuration)
            .CreateLogger();

        var logger = new MainLogger();
        LogProvider.SetCurrentLogger(logger);
        return logger;
    }

    /// <summary>
    /// Configures Serilog with sensible defaults (console + rolling file).
    /// Useful for tooling / tests that don't have a full <see cref="IConfiguration"/>.
    /// </summary>
    public static ILog SetupLogger()
    {
        Log.Logger = new LoggerConfiguration()
            .Enrich.WithThreadId()
            .Enrich.WithProperty("MachineName", Environment.MachineName)
            .Enrich.FromLogContext()
            .MinimumLevel.Verbose()
            .WriteTo.Console()
            .WriteTo.File(
                path: Path.Combine("Logs", "cinema-.log"),
                rollingInterval: RollingInterval.Day,
                outputTemplate: "{MachineName} {ThreadId} {Timestamp:HH:mm:ss} [{Level:u3}] {Message}{NewLine}{Exception}")
            .CreateLogger();

        var logger = new MainLogger();
        LogProvider.SetCurrentLogger(logger);
        return logger;
    }
}
