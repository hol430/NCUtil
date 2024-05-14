namespace NCUtil.Core.Logging;

public interface ILogger
{
    void Log(LogLevel level, string format, params object[] args);
}
