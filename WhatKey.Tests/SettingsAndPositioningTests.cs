using Xunit;
using WhatKey.Models;
using WhatKey.Services;

namespace WhatKey.Tests;

public sealed class SettingsAndPositioningTests
{
    [Fact]
    public void SettingsRoundTripPreservesUserPreferences()
    {
        var path = Path.Combine(Path.GetTempPath(), $"whatkey-{Guid.NewGuid():N}.json");
        try
        {
            var service = new JsonSettingsService(path);
            service.Save(new AppSettings
            {
                Enabled = false,
                OverlayPosition = OverlayPosition.BottomRight,
                OverlayScale = 1.5,
            });

            var loaded = service.Load();

            Assert.False(loaded.Enabled);
            Assert.Equal(OverlayPosition.BottomRight, loaded.OverlayPosition);
            Assert.Equal(1.5, loaded.OverlayScale);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public void SettingsClampInvalidScaleWhenLoaded()
    {
        var path = Path.Combine(Path.GetTempPath(), $"whatkey-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, "{\"OverlayScale\": 4.0}");
            var loaded = new JsonSettingsService(path).Load();
            Assert.Equal(2.0, loaded.OverlayScale);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Theory]
    [InlineData(OverlayPosition.TopLeft, 124, 124)]
    [InlineData(OverlayPosition.TopCenter, 450, 124)]
    [InlineData(OverlayPosition.TopRight, 776, 124)]
    [InlineData(OverlayPosition.CenterLeft, 124, 450)]
    [InlineData(OverlayPosition.Center, 450, 450)]
    [InlineData(OverlayPosition.CenterRight, 776, 450)]
    [InlineData(OverlayPosition.BottomLeft, 124, 776)]
    [InlineData(OverlayPosition.BottomCenter, 450, 776)]
    [InlineData(OverlayPosition.BottomRight, 776, 776)]
    public void PositionCalculationSupportsAllNinePositions(OverlayPosition position, int expectedX, int expectedY)
    {
        var result = OverlayPositionCalculator.Calculate(new ScreenArea(100, 100, 1000, 800), 300, 100, position);
        Assert.Equal(new OverlayCoordinates(expectedX, expectedY), result);
    }

    [Fact]
    public void DisabledSettingsSuppressOverlay()
    {
        Assert.False(OverlayVisibilityPolicy.ShouldShow(new AppSettings { Enabled = false }));
        Assert.True(OverlayVisibilityPolicy.ShouldShow(new AppSettings { Enabled = true }));
    }
}
