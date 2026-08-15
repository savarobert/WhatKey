using Microsoft.Extensions.Configuration;
using Serilog.Events;

namespace WhatKey.Services;

public sealed class ApplicationConfiguration
{
    public LoggingOptions Logging { get; init; } = new();
}

public sealed class LoggingOptions
{
    public LogEventLevel MinimumLevel { get; init; } = LogEventLevel.Debug;
    public int RetentionDays { get; init; } = 14;
    public long FileSizeLimitBytes { get; init; } = 10 * 1024 * 1024;
}

public sealed record ApplicationConfigurationLoadResult(
    ApplicationConfiguration Configuration,
    string? Warning,
    Exception? Exception);

public static class ApplicationConfigurationLoader
{
    public static ApplicationConfigurationLoadResult Load(string? appSettingsPath = null)
    {
        var path = appSettingsPath ?? ApplicationPaths.AppSettingsPath;
        if (!File.Exists(path))
        {
            return new ApplicationConfigurationLoadResult(
                new ApplicationConfiguration(),
                $"Application configuration file was not found at {path}; using built-in defaults.",
                null);
        }

        try
        {
            var directory = Path.GetDirectoryName(path) ?? ApplicationPaths.BaseDirectory;
            var fileName = Path.GetFileName(path);
            var configuration = new ConfigurationBuilder()
                .SetBasePath(directory)
                .AddJsonFile(fileName, optional: false, reloadOnChange: false)
                .Build();

            return new ApplicationConfigurationLoadResult(
                ReadConfiguration(configuration),
                null,
                null);
        }
        catch (Exception exception)
        {
            return new ApplicationConfigurationLoadResult(
                new ApplicationConfiguration(),
                $"Application configuration at {path} could not be loaded; using built-in defaults.",
                exception);
        }
    }

    private static ApplicationConfiguration ReadConfiguration(IConfiguration configuration)
    {
        var logging = configuration.GetSection("Logging");
        var defaults = new LoggingOptions();

        return new ApplicationConfiguration
        {
            Logging = new LoggingOptions
            {
                MinimumLevel = ParseLogLevel(logging["MinimumLevel"], defaults.MinimumLevel),
                RetentionDays = ParseInt(logging["RetentionDays"], defaults.RetentionDays, 1, 3650),
                FileSizeLimitBytes = ParseInt(logging["FileSizeLimitMb"], (int)(defaults.FileSizeLimitBytes / (1024 * 1024)), 1, 1024) * 1024L * 1024L,
            },
        };
    }

    private static LogEventLevel ParseLogLevel(string? value, LogEventLevel fallback)
        => Enum.TryParse(value, ignoreCase: true, out LogEventLevel result) && Enum.IsDefined(result)
            ? result
            : fallback;

    private static int ParseInt(string? value, int fallback, int minimum, int maximum)
        => int.TryParse(value, out var result)
            ? Math.Clamp(result, minimum, maximum)
            : fallback;
}
