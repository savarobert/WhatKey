using Xunit;
using WhatKey.Models;
using WhatKey.Services;

namespace WhatKey.Tests;

public sealed class SettingsAndPositioningTests
{
    [Fact]
    public void MonitorFactorySelectsTheCurrentOperatingSystemImplementation()
    {
        using var monitor = LockKeyMonitorFactory.Create();

        if (OperatingSystem.IsWindows())
            Assert.IsType<WindowsLockKeyMonitor>(monitor);
        else if (OperatingSystem.IsLinux())
            Assert.IsType<LinuxLockKeyMonitor>(monitor);
        else if (OperatingSystem.IsMacOS())
            Assert.IsType<MacOSLockKeyMonitor>(monitor);
        else
            Assert.IsType<UnsupportedLockKeyMonitor>(monitor);
    }

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
                OverlayDurationMs = 2200,
            });

            var loaded = service.Load();

            Assert.False(loaded.Enabled);
            Assert.Equal(OverlayPosition.BottomRight, loaded.OverlayPosition);
            Assert.Equal(1.5, loaded.OverlayScale);
            Assert.Equal(2200, loaded.OverlayDurationMs);
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

    [Fact]
    public void MissingSettingsUseDefaults()
    {
        var path = Path.Combine(Path.GetTempPath(), $"whatkey-{Guid.NewGuid():N}.json");
        try
        {
            var loaded = new JsonSettingsService(path).Load();

            Assert.True(loaded.Enabled);
            Assert.Equal(OverlayPosition.TopCenter, loaded.OverlayPosition);
            Assert.Equal(1.0, loaded.OverlayScale);
            Assert.Equal(AppSettings.DefaultOverlayDurationMs, loaded.OverlayDurationMs);
            Assert.True(File.Exists(path));
            Assert.Contains("\"OverlayDurationMs\": 1300", File.ReadAllText(path));
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public void OlderSettingsUseDefaultDurationAndPreserveOtherValues()
    {
        var path = Path.Combine(Path.GetTempPath(), $"whatkey-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, "{\"Enabled\":false,\"OverlayPosition\":8,\"OverlayScale\":1.5}");

            var loaded = new JsonSettingsService(path).Load();

            Assert.False(loaded.Enabled);
            Assert.Equal(OverlayPosition.BottomRight, loaded.OverlayPosition);
            Assert.Equal(1.5, loaded.OverlayScale);
            Assert.Equal(AppSettings.DefaultOverlayDurationMs, loaded.OverlayDurationMs);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(5001)]
    public void InvalidDurationFallsBackToDefault(int durationMs)
    {
        var path = Path.Combine(Path.GetTempPath(), $"whatkey-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, $"{{\"OverlayDurationMs\":{durationMs}}}");

            var loaded = new JsonSettingsService(path).Load();

            Assert.Equal(AppSettings.DefaultOverlayDurationMs, loaded.OverlayDurationMs);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public void MalformedSettingsUseDefaults()
    {
        var path = Path.Combine(Path.GetTempPath(), $"whatkey-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, "{ malformed settings");
            var loaded = new JsonSettingsService(path).Load();

            Assert.True(loaded.Enabled);
            Assert.Equal(OverlayPosition.TopCenter, loaded.OverlayPosition);
            Assert.Equal(1.0, loaded.OverlayScale);
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
