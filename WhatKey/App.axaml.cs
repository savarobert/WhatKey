using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using WhatKey.Services;

namespace WhatKey;

public partial class App : Avalonia.Application
{
    private TrayApplicationController? _controller;
    private ILogger<App>? _logger;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var loggerFactory = Program.LoggerFactory ?? NullLoggerFactory.Instance;
        _logger = loggerFactory.CreateLogger<App>();
        Dispatcher.UIThread.UnhandledException += OnDispatcherUnhandledException;

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _controller = new TrayApplicationController(desktop, loggerFactory);
            _controller.Start();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnDispatcherUnhandledException(object? sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _logger?.LogError(e.Exception, "Unhandled Avalonia UI exception");
        // Keep Handled false so fatal UI exceptions retain their normal behavior.
    }
}
