using Serilog;
using Serilog.Events;

namespace PrintAgent.Logging;

public static class LoggerSetup
{
    public static ILogger Create(string logsDirectory, string minimumLevel)
    {
        var level = Enum.TryParse<LogEventLevel>(minimumLevel, true, out var parsed)
            ? parsed
            : LogEventLevel.Information;

        Directory.CreateDirectory(logsDirectory);

        return new LoggerConfiguration()
            .MinimumLevel.Is(level)
            .Enrich.WithProperty("App", "PrintAgent")
            .WriteTo.Console()
            .WriteTo.File(
                path: Path.Combine(logsDirectory, "printagent-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                shared: true)
            .CreateLogger();
    }
}
