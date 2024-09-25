using NCUtil.Core.Extensions;
using NCUtil.Core.MPI;

namespace NCUtil.Core.Logging;

public class LogFile : ILogger
{
    private readonly LogLevel verbosity;
    private readonly bool showProgress;
    private readonly TextWriter output;
    private readonly TextWriter error;
    private readonly DateTime startTime = DateTime.Now;
    private readonly char progressEol;
    private readonly TimeSpan progressInterval;

    /// <summary>True to prefix log messages with MPI rank.</summary>
    private readonly bool mpi;

    /// <summary>MPI rank, or 0 if MPI not being used.</summary>
    private readonly int rank;

    private DateTime progressStartTime = DateTime.Now;
    private DateTime lastProgressReport = DateTime.MinValue;

    public LogFile(LogLevel verbosity, bool showProgress, int progressInterval
        , bool mpi)
    {
        this.verbosity = verbosity;
        this.showProgress = showProgress;
        output = Console.Out;
        error = Console.Error;
        progressEol = Console.IsOutputRedirected ? '\n' : '\r';
        this.progressInterval = TimeSpan.FromSeconds(progressInterval);
        this.mpi = mpi;
        rank = Mpi.MPI_Comm_rank(MpiBridge.MPI_COMM_WORLD);
    }

    public void Log(LogLevel level, string format, params object[] args)
    {
        if (level <= verbosity)
        {
            string message = string.Format(format, args);
            TextWriter writer = level == LogLevel.Error ? error : output;
            if (mpi)
                writer.Write("[slave {0}] ", rank);
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

        DateTime now = DateTime.Now;
        TimeSpan timeSinceLog = now - lastProgressReport;
        if (timeSinceLog < progressInterval)
            return;

        lastProgressReport = now;
        double percent = 100.0 * progress;
        TimeSpan elapsed = now - progressStartTime;
        TimeSpan expected = progress != 0 ? elapsed / progress : TimeSpan.MaxValue;
        TimeSpan remaining = expected - elapsed;
        output.Write($"Working: {percent:f2}%; Elapsed: {elapsed.ToReadableString()}; Remaining: {remaining.ToReadableString()}{progressEol}");
    }

    public void InitWallTime()
    {
        progressStartTime = DateTime.Now;
    }
}
