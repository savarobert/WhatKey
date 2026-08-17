using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using Microsoft.Extensions.Logging;
using System.Reactive.Linq;
using WhatKey.Models;
using WhatKey.ViewModels;
using WhatKey.Views;

namespace WhatKey.Services;

public sealed class TrayApplicationController : IDisposable
{
    private readonly IClassicDesktopStyleApplicationLifetime _desktop;
    private readonly ILogger<TrayApplicationController> _logger;
    private readonly ISettingsService _settingsService;
    private readonly ILockKeyMonitor _lockKeyMonitor;
    private readonly IOverlayService _overlayService;
    private readonly AppSettings _settings;
    private readonly NativeMenuItem _enabledMenuItem;
    private IDisposable? _lockKeySubscription;
    private IDisposable? _settingsSubscription;
    private TrayIcon? _trayIcon;
    private SettingsWindow? _settingsWindow;
    private SettingsViewModel? _settingsViewModel;
    private bool _isDisposed;

    public TrayApplicationController(IClassicDesktopStyleApplicationLifetime desktop, ILoggerFactory loggerFactory)
    {
        _desktop = desktop;
        _logger = loggerFactory.CreateLogger<TrayApplicationController>();
        _settingsService = new JsonSettingsService(logger: loggerFactory.CreateLogger<JsonSettingsService>());
        _settings = _settingsService.Load();
        _lockKeyMonitor = LockKeyMonitorFactory.Create(loggerFactory);
        _overlayService = new OverlayService(loggerFactory.CreateLogger<OverlayService>());
        _enabledMenuItem = new NativeMenuItem("Enabled")
        {
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked = _settings.Enabled,
        };
    }

    public void Start()
    {
        _logger.LogInformation("Starting tray application lifetime");
        _desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        _desktop.Exit += OnDesktopExit;
        _enabledMenuItem.Click += OnEnabledClicked;
        _lockKeySubscription = _overlayService.Bind(_lockKeyMonitor.StateChanges);
        _overlayService.ApplySettings(_settings);
        _lockKeyMonitor.Start();

        var menu = new NativeMenu();
        var settingsItem = new NativeMenuItem("Settings");
        settingsItem.Click += (_, _) => ShowSettings();
        var exitItem = new NativeMenuItem("Exit");
        exitItem.Click += (_, _) => _desktop.Shutdown();
        menu.Items.Add(settingsItem);
        menu.Items.Add(_enabledMenuItem);
        menu.Items.Add(exitItem);

        _trayIcon = new TrayIcon
        {
            Icon = LoadIcon(),
            ToolTipText = "WhatKey",
            Menu = menu,
            IsVisible = true,
        };
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        _lockKeySubscription?.Dispose();
        _lockKeySubscription = null;
        _settingsSubscription?.Dispose();
        _settingsSubscription = null;
        _lockKeyMonitor.Dispose();
        _overlayService.Dispose();
        _settingsViewModel?.Save();
        _settingsWindow?.Close();
        _trayIcon?.Dispose();
        _desktop.Exit -= OnDesktopExit;
        _logger.LogInformation("Tray application lifetime stopped");
    }

    private void OnDesktopExit(object? sender, ControlledApplicationLifetimeExitEventArgs e) => Dispose();

    private void OnEnabledClicked(object? sender, EventArgs e)
    {
        _settings.Enabled = !_settings.Enabled;
        _enabledMenuItem.IsChecked = _settings.Enabled;
        _settingsService.Save(_settings);
        _overlayService.ApplySettings(_settings);
        _logger.LogInformation("Lock-key overlays {State}", _settings.Enabled ? "enabled" : "disabled");
    }

    private void ShowSettings()
    {
        if (_settingsWindow is not null)
        {
            _settingsWindow.Show();
            _settingsWindow.Activate();
            return;
        }

        _settingsViewModel = new SettingsViewModel(_settingsService, _settings);
        _settingsSubscription = _settingsViewModel.SettingsChanges
            .Subscribe(_ => _overlayService.ApplySettings(_settings));
        _settingsWindow = new SettingsWindow { DataContext = _settingsViewModel };
        _settingsWindow.Closed += OnSettingsClosed;
        _settingsWindow.Show();
    }

    private void OnSettingsClosed(object? sender, EventArgs e)
    {
        _settingsSubscription?.Dispose();
        _settingsSubscription = null;
        _settingsViewModel?.Dispose();
        _settingsWindow!.Closed -= OnSettingsClosed;
        _settingsWindow = null;
        _settingsViewModel = null;
    }

    private static WindowIcon? LoadIcon()
    {
        try
        {
            using var stream = AssetLoader.Open(new Uri("avares://WhatKey/Assets/avalonia-logo.ico"));
            return new WindowIcon(stream);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
