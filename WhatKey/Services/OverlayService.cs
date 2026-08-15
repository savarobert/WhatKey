using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using WhatKey.Models;
using WhatKey.ViewModels;
using WhatKey.Views;

namespace WhatKey.Services;

public sealed class OverlayService : IOverlayService
{
    private const double BaseWidth = 300;
    private const double BaseHeight = 82;
    private readonly OverlayWindow _window;
    private readonly ILogger<OverlayService> _logger;
    private readonly OverlayViewModel _viewModel;
    private readonly IWindowTopmostService _windowTopmostService;
    private CancellationTokenSource? _dismissCancellation;
    private AppSettings _settings = new();

    public OverlayService(ILogger<OverlayService>? logger = null)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<OverlayService>.Instance;
        _viewModel = new OverlayViewModel();
        _window = new OverlayWindow { DataContext = _viewModel };
        _windowTopmostService = WindowTopmostServiceFactory.Create(_logger);
    }

    public void ApplySettings(AppSettings settings)
    {
        _settings = settings;
        _viewModel.Scale = settings.OverlayScale;
        _window.Width = BaseWidth * _viewModel.Scale;
        _window.Height = BaseHeight * _viewModel.Scale;

        if (_window.IsVisible)
            PositionWindow();
    }

    public void Show(LockKey key, bool isOn)
    {
        if (!OverlayVisibilityPolicy.ShouldShow(_settings))
            return;

        Dispatcher.UIThread.Post(() => _ = ShowOnUiThreadAsync(key, isOn));
    }

    public void Dispose()
    {
        _dismissCancellation?.Cancel();
        _dismissCancellation?.Dispose();
        _window.Close();
    }

    private async Task ShowOnUiThreadAsync(LockKey key, bool isOn)
    {
        if (!OverlayVisibilityPolicy.ShouldShow(_settings))
            return;

        await _dismissCancellation?.CancelAsync()!;
        _dismissCancellation?.Dispose();
        _dismissCancellation = new CancellationTokenSource();
        var cancellationToken = _dismissCancellation.Token;

        _viewModel.Show(key, isOn);
        _logger.LogDebug("Showing lock-key overlay for {Key}; enabled state: {IsOn}", key, isOn);
        if (!_window.IsVisible)
        {
            _window.Opacity = 0;
            _window.Topmost = true;
            _window.Show();
            PositionWindow();
            _windowTopmostService.EnsureTopmost(_window);
            _window.Opacity = 1;
        }
        else
        {
            PositionWindow();
            _window.Topmost = true;
            _windowTopmostService.EnsureTopmost(_window);
        }

        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(1300), cancellationToken);
            if (!cancellationToken.IsCancellationRequested)
                _window.Hide();
        }
        catch (OperationCanceledException)
        {
            // A newer key event restarted the overlay timeout.
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to display lock-key overlay");
        }
    }

    private void PositionWindow()
    {
        var width = (int)Math.Ceiling(_window.Width);
        var height = (int)Math.Ceiling(_window.Height);
        var workArea = ScreenPositioningService.GetActiveWorkArea(_window);
        var position = ScreenPositioningService.CalculatePosition(workArea, width, height, _settings.OverlayPosition);
        _window.Position = new PixelPoint(position.X, position.Y);
    }
}
