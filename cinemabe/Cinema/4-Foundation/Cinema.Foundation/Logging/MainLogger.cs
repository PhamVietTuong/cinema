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
        try { Log.Logger.Verbose(messageTemplate); } catch { }
    }
    public void Verbose(Exception exception, string messageTemplate)
    {
        try { Log.Logger.Verbose(exception, messageTemplate); } catch { }
    }

    public void Debug(string messageTemplate)
    {
        try { Log.Logger.Debug(messageTemplate); } catch { }
    }
    public void Debug(Exception exception, string messageTemplate)
    {
        try { Log.Logger.Debug(exception, messageTemplate); } catch { }
    }

    public void Information(string messageTemplate)
    {
        try { Log.Logger.Information(messageTemplate); } catch { }
    }
    public void Information(Exception exception, string messageTemplate)
    {
        try { Log.Logger.Information(exception, messageTemplate); } catch { }
    }

    public void Warning(string messageTemplate)
    {
        try { Log.Logger.Warning(messageTemplate); } catch { }
    }
    public void Warning(Exception exception, string messageTemplate)
    {
        try { Log.Logger.Warning(exception, messageTemplate); } catch { }
    }

    public void Error(string messageTemplate)
    {
        try { Log.Logger.Error(messageTemplate); } catch { }
    }
    public void Error(Exception exception, string messageTemplate)
    {
        try { Log.Logger.Error(exception, messageTemplate); } catch { }
    }

    public void Fatal(string messageTemplate)
    {
        try { Log.Logger.Fatal(messageTemplate); } catch { }
    }
    public void Fatal(Exception exception, string messageTemplate)
    {
        try { Log.Logger.Fatal(exception, messageTemplate); } catch { }
    }

    public void Write(LogEventLevel level, string messageTemplate)
    {
        try
        {
            var serilogLevel = (Serilog.Events.LogEventLevel)Enum.Parse(typeof(Serilog.Events.LogEventLevel), level.ToString());
            Log.Logger.Write(serilogLevel, messageTemplate);
        }
        catch { }
    }
    public void Write(LogEventLevel level, Exception exception, string messageTemplate)
    {
        try
        {
            var serilogLevel = (Serilog.Events.LogEventLevel)Enum.Parse(typeof(Serilog.Events.LogEventLevel), level.ToString());
            Log.Logger.Write(serilogLevel, exception, messageTemplate);
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
