using System.CommandLine;
using Microsoft.Extensions.Logging;
using OraPgMigrator.Infrastructure.Logging;

namespace OraPgMigrator.Cli;

/// <summary>Builds a console(+optional file) logger for one command invocation from --log-level/--log-file.</summary>
public static class CliHost
{
    public static ILogger CreateLogger(ParseResult parseResult, Option<LogLevel> logLevelOption, Option<string?> logFileOption)
    {
        var level = parseResult.GetValue(logLevelOption);
        var logFile = parseResult.GetValue(logFileOption);

        var factory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(level);
            builder.AddSimpleConsole(o =>
            {
                o.SingleLine = true;
                o.TimestampFormat = "HH:mm:ss ";
            });
            if (!string.IsNullOrWhiteSpace(logFile))
            {
                builder.AddProvider(new FileLoggerProvider(logFile));
            }
        });

        // The factory is intentionally not disposed with the logger it hands out; console/file
        // providers flush per-write and the process is short-lived per CLI invocation.
        return factory.CreateLogger("orapg");
    }
}
