using Serilog;

namespace Cinema.Foundation.Logging;

/// <summary>
/// Core logging API — wraps Serilog. Methods are guaranteed never to throw.
/// </summary>
public class MainLogger : ILog
{
    public MainLogger() { }

    public void Verbose(string messageTemplate)
    {
        try { Log.Logger.Verbose(LogSanitizer.Neutralize(messageTemplate)); } catch { }
    }
    public void Verbose(Exception exception, string messageTemplate)
    {
        try { Log.Logger.Verbose(exception, LogSanitizer.Neutralize(messageTemplate)); } catch { }
    }

    public void Debug(string messageTemplate)
    {
        try { Log.Logger.Debug(LogSanitizer.Neutralize(messageTemplate)); } catch { }
    }
    public void Debug(Exception exception, string messageTemplate)
    {
        try { Log.Logger.Debug(exception, LogSanitizer.Neutralize(messageTemplate)); } catch { }
    }

    public void Information(string messageTemplate)
    {
        try { Log.Logger.Information(LogSanitizer.Neutralize(messageTemplate)); } catch { }
    }
    public void Information(Exception exception, string messageTemplate)
    {
        try { Log.Logger.Information(exception, LogSanitizer.Neutralize(messageTemplate)); } catch { }
    }

    public void Warning(string messageTemplate)
    {
        try { Log.Logger.Warning(LogSanitizer.Neutralize(messageTemplate)); } catch { }
    }
    public void Warning(Exception exception, string messageTemplate)
    {
        try { Log.Logger.Warning(exception, LogSanitizer.Neutralize(messageTemplate)); } catch { }
    }

    public void Error(string messageTemplate)
    {
        try { Log.Logger.Error(LogSanitizer.Neutralize(messageTemplate)); } catch { }
    }
    public void Error(Exception exception, string messageTemplate)
    {
        try { Log.Logger.Error(exception, LogSanitizer.Neutralize(messageTemplate)); } catch { }
    }

    public void Fatal(string messageTemplate)
    {
        try { Log.Logger.Fatal(LogSanitizer.Neutralize(messageTemplate)); } catch { }
    }
    public void Fatal(Exception exception, string messageTemplate)
    {
        try { Log.Logger.Fatal(exception, LogSanitizer.Neutralize(messageTemplate)); } catch { }
    }

    public void Write(LogEventLevel level, string messageTemplate)
    {
        try
        {
            var serilogLevel = (Serilog.Events.LogEventLevel)Enum.Parse(typeof(Serilog.Events.LogEventLevel), level.ToString());
            Log.Logger.Write(serilogLevel, LogSanitizer.Neutralize(messageTemplate));
        }
        catch { }
    }
    public void Write(LogEventLevel level, Exception exception, string messageTemplate)
    {
        try
        {
            var serilogLevel = (Serilog.Events.LogEventLevel)Enum.Parse(typeof(Serilog.Events.LogEventLevel), level.ToString());
            Log.Logger.Write(serilogLevel, exception, LogSanitizer.Neutralize(messageTemplate));
        }
        catch { }
    }

    public bool IsEnabled(LogEventLevel level)
    {
        try
        {
            var serilogLevel = (Serilog.Events.LogEventLevel)Enum.Parse(typeof(Serilog.Events.LogEventLevel), level.ToString());
            return Log.Logger.IsEnabled(serilogLevel);
        }
        catch { return false; }
    }
}
