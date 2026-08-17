using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using System.Reactive.Linq;
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
    private bool _disposed;

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

    public IDisposable Bind(IObservable<LockKeyChangedEventArgs> updates)
    {
        return updates
            .Subscribe(
                update =>
                {
                    _logger.LogDebug(
                        "Lock-key event received by overlay pipeline: {Key} IsOn={IsOn}",
                        update.Key,
                        update.IsOn);

                    try
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            var enabled = OverlayVisibilityPolicy.ShouldShow(_settings);
                            _logger.LogDebug(
                                "Overlay requested: {Key} {State}; enabled state: {Enabled}",
                                update.Key,
                                update.IsOn ? "ON" : "OFF",
                                enabled);

                            if (enabled && !_disposed)
                                _ = ShowOnUiThreadAsync(update.Key, update.IsOn);
                        });
                        _logger.LogDebug("Dispatched overlay request to Avalonia UI thread");
                    }
                    catch (Exception exception)
                    {
                        _logger.LogError(exception, "Failed to dispatch lock-key overlay request");
                    }
                },
                exception => _logger.LogError(exception, "Lock-key overlay update stream failed"));
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _dismissCancellation?.Cancel();
        _dismissCancellation?.Dispose();
        _window.Close();
    }

    private async Task ShowOnUiThreadAsync(LockKey key, bool isOn)
    {
        try
        {
            if (_disposed || !OverlayVisibilityPolicy.ShouldShow(_settings))
                return;

            var previousDismissal = _dismissCancellation;
            var currentDismissal = new CancellationTokenSource();
            _dismissCancellation = currentDismissal;
            if (previousDismissal is not null)
            {
                await previousDismissal.CancelAsync();
                previousDismissal.Dispose();
            }

            // A newer event may have replaced this source while the previous
            // cancellation callbacks were completing asynchronously.
            if (_disposed || !ReferenceEquals(currentDismissal, _dismissCancellation))
            {
                currentDismissal.Dispose();
                return;
            }

            var cancellationToken = currentDismissal.Token;

            _logger.LogDebug("Executing overlay show on Avalonia UI thread for {Key} IsOn={IsOn}", key, isOn);
            _viewModel.Show(key, isOn);
            if (!_window.IsVisible)
            {
                _window.Opacity = 0;
                _window.Topmost = true;
                _window.Show();
                _logger.LogDebug("OverlayWindow.Show completed; IsVisible={IsVisible}", _window.IsVisible);
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

            _logger.LogDebug("Overlay topmost state reasserted; IsVisible={IsVisible}", _window.IsVisible);

            await Task.Delay(TimeSpan.FromMilliseconds(_settings.OverlayDurationMs), cancellationToken);
            if (!cancellationToken.IsCancellationRequested && !_disposed)
            {
                _window.Hide();
                _logger.LogDebug("OverlayWindow.Hide completed; IsVisible={IsVisible}", _window.IsVisible);
            }
        }
        catch (OperationCanceledException)
        {
            // A newer key event restarted the overlay timeout.
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to display lock-key overlay for {Key}", key);
        }
    }

    private void PositionWindow()
    {
        var width = (int)Math.Ceiling(_window.Width);
        var height = (int)Math.Ceiling(_window.Height);
        var workArea = ScreenPositioningService.GetActiveWorkArea(_window);
        var position = ScreenPositioningService.CalculatePosition(workArea, width, height, _settings.OverlayPosition);
        _window.Position = new PixelPoint(position.X, position.Y);
        _logger.LogDebug(
            "Overlay position calculated: X={X}, Y={Y}, Width={Width}, Height={Height}, WorkArea=({WorkAreaX},{WorkAreaY},{WorkAreaWidth},{WorkAreaHeight})",
            position.X,
            position.Y,
            width,
            height,
            workArea.X,
            workArea.Y,
            workArea.Width,
            workArea.Height);
    }
}
