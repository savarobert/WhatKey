using Avalonia;
using Microsoft.Extensions.Logging;
using ReactiveUI.Avalonia;
using System.Reflection;
using WhatKey.Services;

namespace WhatKey;

sealed class Program
{
    internal static ILoggerFactory? LoggerFactory { get; private set; }

    [STAThread]
    public static void Main(string[] args)
    {
        using var logging = LoggingBootstrapper.Create();
        LoggerFactory = logging.LoggerFactory;
        LoggingBootstrapper.ConfigureAvaloniaLogging(logging.LoggerFactory);

        var logger = logging.LoggerFactory.CreateLogger<Program>();
        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
            logger.LogCritical(eventArgs.ExceptionObject as Exception, "Unhandled application exception");
        TaskScheduler.UnobservedTaskException += (_, eventArgs) =>
            logger.LogError(eventArgs.Exception, "Unobserved task exception");

        var platformName = OperatingSystem.IsWindows() ? "Windows" :
                           OperatingSystem.IsLinux() ? "Linux" :
                           OperatingSystem.IsMacOS() ? "macOS" :
                           "Unknown";

        logger.LogInformation(
            "Application starting. Version: {Version}; Platform: {Platform}; LogDirectory: {LogDirectory}",
            GetApplicationVersion(),
            platformName,
            logging.LogDirectory);

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception exception)
        {
            logger.LogCritical(exception, "Application terminated unexpectedly");
            throw;
        }
        finally
        {
            logger.LogInformation("Application shutting down");
            LoggerFactory = null;
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .UseReactiveUI(_ => { });

    private static string GetApplicationVersion()
    {
        var assembly = typeof(Program).Assembly;
        var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        return !string.IsNullOrWhiteSpace(informationalVersion)
            ? informationalVersion
            : assembly.GetName().Version?.ToString() ?? "unknown";
    }
}
