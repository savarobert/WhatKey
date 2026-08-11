using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using WhatKey.Models;
using WhatKey.ViewModels;
using WhatKey.Views;

namespace WhatKey.Services;

public sealed class TrayApplicationController : IDisposable
{
    private readonly IClassicDesktopStyleApplicationLifetime _desktop;
    private readonly ISettingsService _settingsService;
    private readonly ILockKeyMonitor _lockKeyMonitor;
    private readonly IOverlayService _overlayService;
    private readonly AppSettings _settings;
    private readonly NativeMenuItem _enabledMenuItem;
    private TrayIcon? _trayIcon;
    private SettingsWindow? _settingsWindow;
    private SettingsViewModel? _settingsViewModel;
    private bool _isDisposed;

    public TrayApplicationController(IClassicDesktopStyleApplicationLifetime desktop)
    {
        _desktop = desktop;
        _settingsService = new JsonSettingsService();
        _settings = _settingsService.Load();
        _lockKeyMonitor = LockKeyMonitorFactory.Create();
        _overlayService = new OverlayService(new ScreenPositioningService());
        _enabledMenuItem = new NativeMenuItem("Enabled")
        {
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked = _settings.Enabled,
        };
    }

    public void Start()
    {
        _desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        _desktop.Exit += OnDesktopExit;
        _enabledMenuItem.Click += OnEnabledClicked;
        _lockKeyMonitor.StateChanged += OnLockKeyStateChanged;
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
        _lockKeyMonitor.StateChanged -= OnLockKeyStateChanged;
        _lockKeyMonitor.Dispose();
        _overlayService.Dispose();
        _settingsViewModel?.Save();
        _settingsWindow?.Close();
        _trayIcon?.Dispose();
        _desktop.Exit -= OnDesktopExit;
    }

    private void OnDesktopExit(object? sender, ControlledApplicationLifetimeExitEventArgs e) => Dispose();

    private void OnEnabledClicked(object? sender, EventArgs e)
    {
        _settings.Enabled = !_settings.Enabled;
        _enabledMenuItem.IsChecked = _settings.Enabled;
        _settingsService.Save(_settings);
        _overlayService.ApplySettings(_settings);
    }

    private void OnLockKeyStateChanged(object? sender, LockKeyChangedEventArgs e)
    {
        if (OverlayVisibilityPolicy.ShouldShow(_settings))
            _overlayService.Show(e.Key, e.IsOn);
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
        _settingsViewModel.SettingsChanged += OnSettingsChanged;
        _settingsWindow = new SettingsWindow { DataContext = _settingsViewModel };
        _settingsWindow.Closed += OnSettingsClosed;
        _settingsWindow.Show();
    }

    private void OnSettingsChanged()
    {
        _overlayService.ApplySettings(_settings);
    }

    private void OnSettingsClosed(object? sender, EventArgs e)
    {
        if (_settingsViewModel is not null)
            _settingsViewModel.SettingsChanged -= OnSettingsChanged;
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
