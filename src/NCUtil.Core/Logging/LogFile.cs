namespace NCUtil.Core.Logging;

public class LogFile : ILogger
{
    private readonly LogLevel verbosity;
    private readonly bool showProgress;
    private readonly TextWriter output;
    private readonly TextWriter error;
    private readonly DateTime startTime = DateTime.Now;
    private DateTime progressStartTime = DateTime.Now;
    private readonly char progressEol;

    public LogFile(LogLevel verbosity, bool showProgress)
    {
        this.verbosity = verbosity;
        this.showProgress = showProgress;
        output = Console.Out;
        error = Console.Error;
        progressEol = Console.IsOutputRedirected ? '\n' : '\r';
    }

    public void Log(LogLevel level, string format, params object[] args)
    {
        if (level <= verbosity)
        {
            string message = string.Format(format, args);
            TextWriter writer = level == LogLevel.Error ? error : output;
            writer.WriteLine(message);
        }
    }

    /// <summary>
    /// Write a progress report, iff progress reporting is enabled.
    /// </summary>
    /// <param name="progress">Current progress in range [0, 1].</param>
    public void LogProgress(double progress)
    {
        if (!showProgress)
            return;

        if (progress < 0)
        {
            Log(LogLevel.Warning, $"Progress is negative: {progress}");
            return;
        }

        if (progress > 1)
        {
            Log(LogLevel.Warning, $"Progress is >1: {progress}");
            return;
        }

        double percent = 100.0 * progress;
        TimeSpan elapsed = DateTime.Now - progressStartTime;
        TimeSpan expected = elapsed / progress;
        TimeSpan remaining = expected - elapsed;
        output.Write($"Working: {percent:f2}%; Elapsed: {elapsed}; Remaining: {remaining}{progressEol}");
    }

    public void InitWallTime()
    {
        progressStartTime = DateTime.Now;
    }
}
