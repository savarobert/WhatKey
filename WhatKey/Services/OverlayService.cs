using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using WhatKey.Models;
using WhatKey.ViewModels;
using WhatKey.Views;

namespace WhatKey.Services;

public sealed class OverlayService : IOverlayService
{
    private const double BaseWidth = 300;
    private const double BaseHeight = 82;
    private readonly OverlayWindow _window;
    private readonly OverlayViewModel _viewModel;
    private readonly ScreenPositioningService _positioningService;
    private CancellationTokenSource? _dismissCancellation;
    private AppSettings _settings = new();

    public OverlayService(ScreenPositioningService positioningService)
    {
        _positioningService = positioningService;
        _viewModel = new OverlayViewModel();
        _window = new OverlayWindow { DataContext = _viewModel };
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

        _dismissCancellation?.Cancel();
        _dismissCancellation?.Dispose();
        _dismissCancellation = new CancellationTokenSource();
        var cancellationToken = _dismissCancellation.Token;

        _viewModel.Show(key, isOn);
        PositionWindow();
        if (!_window.IsVisible)
            _window.Show();
        else
            _window.Topmost = true;

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
    }

    private void PositionWindow()
    {
        var width = (int)Math.Ceiling(_window.Width);
        var height = (int)Math.Ceiling(_window.Height);
        var workArea = _positioningService.GetActiveWorkArea();
        var position = _positioningService.CalculatePosition(workArea, width, height, _settings.OverlayPosition);
        _window.Position = new PixelPoint(position.X, position.Y);
    }
}
