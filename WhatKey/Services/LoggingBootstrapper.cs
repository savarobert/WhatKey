using Avalonia.Logging;
using Microsoft.Extensions.Logging;
using Serilog;
using AvaloniaLogEventLevel = Avalonia.Logging.LogEventLevel;
using AvaloniaLogger = Avalonia.Logging.Logger;
using MicrosoftLogLevel = Microsoft.Extensions.Logging.LogLevel;
using MicrosoftLogger = Microsoft.Extensions.Logging.ILogger;
using SerilogLogEventLevel = Serilog.Events.LogEventLevel;

namespace WhatKey.Services;

public sealed class LoggingContext : IDisposable
{
    private readonly Serilog.Core.Logger _serilogLogger;

    internal LoggingContext(ILoggerFactory loggerFactory, Serilog.Core.Logger serilogLogger, string logDirectory)
    {
        LoggerFactory = loggerFactory;
        _serilogLogger = serilogLogger;
        LogDirectory = logDirectory;
    }

    public ILoggerFactory LoggerFactory { get; }
    public string LogDirectory { get; }

    public void Dispose()
    {
        LoggerFactory.Dispose();
        _serilogLogger.Dispose();
    }
}

public static class LoggingBootstrapper
{
    public static LoggingContext Create()
    {
        var logDirectory = ApplicationPaths.LogDirectory;
        var configuration = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Avalonia", SerilogLogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", ApplicationPaths.ApplicationName);

        try
        {
            Directory.CreateDirectory(logDirectory);
            configuration = configuration.WriteTo.File(
                ApplicationPaths.LogFilePath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                fileSizeLimitBytes: 10 * 1024 * 1024,
                rollOnFileSizeLimit: true,
                shared: true,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}");
        }
        catch (Exception exception)
        {
            // Logging must not prevent the tray application from starting.
            System.Diagnostics.Debug.WriteLine($"WhatKey: persistent logging unavailable: {exception}");
        }

#if DEBUG
        configuration = configuration.WriteTo.Console();
#endif

        var serilogLogger = (Serilog.Core.Logger)configuration.CreateLogger();
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.ClearProviders();
            builder.SetMinimumLevel(LogLevel.Debug);
            builder.AddSerilog(serilogLogger, dispose: false);
        });

        return new LoggingContext(loggerFactory, serilogLogger, logDirectory);
    }

    public static void ConfigureAvaloniaLogging(ILoggerFactory loggerFactory)
    {
        AvaloniaLogger.Sink = new AvaloniaSerilogSink(loggerFactory.CreateLogger("Avalonia"));
    }
}

internal sealed class AvaloniaSerilogSink(MicrosoftLogger logger) : ILogSink
{
    public bool IsEnabled(AvaloniaLogEventLevel level, string area) => logger.IsEnabled(ToMicrosoftLevel(level));

    public void Log(AvaloniaLogEventLevel level, string area, object? source, string messageTemplate)
        => Log(level, area, source, messageTemplate, Array.Empty<object?>());

    public void Log(AvaloniaLogEventLevel level, string area, object? source, string messageTemplate, params object?[] propertyValues)
    {
        var microsoftLevel = ToMicrosoftLevel(level);
        if (!logger.IsEnabled(microsoftLevel))
            return;

        logger.Log(microsoftLevel, "Avalonia {Area}: {MessageTemplate} {@PropertyValues}",
            area, messageTemplate, propertyValues);
    }

    private static MicrosoftLogLevel ToMicrosoftLevel(AvaloniaLogEventLevel level) => level switch
    {
        AvaloniaLogEventLevel.Verbose => MicrosoftLogLevel.Debug,
        AvaloniaLogEventLevel.Debug => MicrosoftLogLevel.Debug,
        AvaloniaLogEventLevel.Information => MicrosoftLogLevel.Information,
        AvaloniaLogEventLevel.Warning => MicrosoftLogLevel.Warning,
        AvaloniaLogEventLevel.Error => MicrosoftLogLevel.Error,
        AvaloniaLogEventLevel.Fatal => MicrosoftLogLevel.Critical,
        _ => MicrosoftLogLevel.Information,
    };
}
