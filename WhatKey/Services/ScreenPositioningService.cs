using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using WhatKey.Models;

namespace WhatKey.Services;

public sealed class ScreenPositioningService
{
    public ScreenArea GetActiveWorkArea(Window window)
    {
        var screens = window.Screens;
        var screen = screens.ScreenFromWindow(window) ?? screens.Primary ?? screens.All.FirstOrDefault();
        var workingArea = screen?.WorkingArea ?? new PixelRect(0, 0, 1920, 1080);
        return new ScreenArea(workingArea.X, workingArea.Y, workingArea.Width, workingArea.Height);
    }

    public OverlayCoordinates CalculatePosition(ScreenArea workArea, int width, int height,
        OverlayPosition position) => OverlayPositionCalculator.Calculate(workArea, width, height, position);
}
