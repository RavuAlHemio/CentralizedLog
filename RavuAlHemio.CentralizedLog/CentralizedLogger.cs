using System;
using System.Collections.Generic;
using System.IO;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Serilog;
using Serilog.Events;
using Serilog.Extensions.Logging;

namespace RavuAlHemio.CentralizedLog
{
    public static class CentralizedLogger
    {
        public static ILoggerFactory Factory { get; set; }

        public static readonly IReadOnlyDictionary<LogLevel, LogEventLevel> SerilogLevelMapping =
            new Dictionary<LogLevel, LogEventLevel>
            {
                [LogLevel.None] = LogEventLevel.Fatal + 1,
                [LogLevel.Critical] = LogEventLevel.Fatal,
                [LogLevel.Error] = LogEventLevel.Error,
                [LogLevel.Warning] = LogEventLevel.Warning,
                [LogLevel.Information] = LogEventLevel.Information,
                [LogLevel.Debug] = LogEventLevel.Debug,
                [LogLevel.Trace] = LogEventLevel.Verbose
            };

        static CentralizedLogger()
        {
            Factory = new LoggerFactory();
        }

        public static void SetupConsoleLogging([CanBeNull] LogLevel? minimumLevel = null,
                [CanBeNull] Dictionary<string, LogLevel> logFilter = null)
        {
            var consoleProvider = new ConsoleLoggerProvider(
                (text, logLevel) => ConsoleLogFilter(text, logLevel, minimumLevel, logFilter),
                true
            );
            Factory.AddProvider(consoleProvider);
        }

        public static void SetupFileLogging([NotNull] string applicationName, [CanBeNull] LogLevel? level = null,
                [CanBeNull] Dictionary<string, LogLevel> logFilter = null)
        {
            var serilogLoggerConfig = new LoggerConfiguration()
                .MinimumLevel.Is(level.HasValue ? SerilogLevelMapping[level.Value] : LogEventLevel.Verbose)
                .Filter.ByIncludingOnly(evt => FileLogFilter(evt, logFilter))
                .Enrich.WithThreadId()
                .WriteTo.RollingFile(
                    pathFormat: Path.Combine(AppContext.BaseDirectory, $"{applicationName}-{{Date}}.log"),
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{ThreadId}] [{Level}] {Message}{NewLine}{Exception}"
                );

            Factory.AddProvider(new SerilogLoggerProvider(serilogLoggerConfig.CreateLogger()));
        }

        static bool ConsoleLogFilter(string logger, LogLevel level, [CanBeNull] LogLevel? minimumLevel = null,
                [CanBeNull] Dictionary<string, LogLevel> logFilter = null)
        {
            if (minimumLevel.HasValue && level < minimumLevel.Value)
            {
                return false;
            }

            if (logFilter != null)
            {
                LogLevel minimumLoggerLevel;
                if (logFilter.TryGetValue(logger, out minimumLoggerLevel))
                {
                    if (level < minimumLoggerLevel)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        static bool FileLogFilter(LogEvent evt, [CanBeNull] Dictionary<string, LogLevel> filter)
        {
            if (filter == null)
            {
                return true;
            }

            LogLevel minimumLevel;
            string className = (string)((ScalarValue)evt.Properties["SourceContext"]).Value;
            if (!filter.TryGetValue(className, out minimumLevel))
            {
                return true;
            }

            return evt.Level >= SerilogLevelMapping[minimumLevel];
        }
    }
}
