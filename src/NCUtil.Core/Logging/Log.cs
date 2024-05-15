namespace NCUtil.Core.Logging;

public static class Log
{
    private static ILogger? logService;

    public static void ConfigureLogging(LogLevel verbosity, bool showProgress)
    {
        logService = new LogFile(verbosity, showProgress);
    }

    public static void Error(string format, params object[] args)
    {
        WriteLog(LogLevel.Error, format, args);
    }

    public static void Warning(string format, params object[] args)
    {
        WriteLog(LogLevel.Warning, format, args);
    }

    public static void Information(string format, params object[] args)
    {
        WriteLog(LogLevel.Information, format, args);
    }

    public static void Diagnostic(string format, params object[] args)
    {
        WriteLog(LogLevel.Diagnostic, format, args);
    }

    public static void Debug(string format, params object[] args)
    {
        WriteLog(LogLevel.Debug, format, args);
    }

    public static void InitWallTime()
    {
        if (logService == null)
            throw new InvalidOperationException($"No logging service has been configured");
        logService.InitWallTime();
    }

    public static void Progress(double progress)
    {
        if (logService == null)
            throw new InvalidOperationException($"No logging service has been configured");
        logService.LogProgress(progress);
    }

    private static void WriteLog(LogLevel level, string format, params object[] args)
    {
        if (logService == null)
            throw new InvalidOperationException($"No logging service has been configured");
        logService.Log(level, format, args);
    }
}
