namespace Cinema.Foundation.Logging;

/// <summary>
/// Exposes a static <see cref="ILog"/> instance accessible anywhere in the application.
/// </summary>
public static class LogProvider
{
    private static ILog _current = new MainLogger();

    /// <summary>Gets the current default logger.</summary>
    public static ILog Current => _current;

    /// <summary>Replaces the current default logger.</summary>
    public static void SetCurrentLogger(ILog logger)
    {
        _current = logger;
    }
}
