using WhatKey.Models;
using WhatKey.Services;
using Xunit;

namespace WhatKey.Tests;

public sealed class SysfsLockKeyTests
{
    [Theory]
    [InlineData("input3::capslock", LockKey.CapsLock)]
    [InlineData("input3::numlock", LockKey.NumLock)]
    [InlineData("platform::scrolllock", LockKey.ScrollLock)]
    [InlineData("CAPSLOCK", LockKey.CapsLock)]
    public void TryParseLedNameRecognizesLockKeySuffix(string name, LockKey expected)
    {
        Assert.True(SysfsLockKeySupport.TryParseLedName(name, out var actual));
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void TryParseLedNameRejectsOtherLeds()
    {
        Assert.False(SysfsLockKeySupport.TryParseLedName("input3::num", out _));
        Assert.False(SysfsLockKeySupport.TryParseLedName(null, out _));
    }

    [Theory]
    [InlineData("0", false)]
    [InlineData("1", true)]
    [InlineData("255", true)]
    [InlineData(" 0\n", false)]
    public void TryParseBrightnessMapsKernelValues(string value, bool expected)
    {
        Assert.True(SysfsLockKeySupport.TryParseBrightness(value, out var actual));
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void SelectLedPathsChoosesOneStablePathPerKey()
    {
        var paths = new[]
        {
            "/sys/class/leds/input5::capslock",
            "/sys/class/leds/input2::capslock",
            "/sys/class/leds/input1::numlock",
            "/sys/class/leds/input4::numlock",
            "/sys/class/leds/input7::scrolllock",
        };

        var selected = SysfsLockKeySupport.SelectLedPaths(paths);

        Assert.Equal(3, selected.Count);
        Assert.Equal(Path.Combine(paths[1], "brightness"), selected[LockKey.CapsLock]);
        Assert.Equal(Path.Combine(paths[2], "brightness"), selected[LockKey.NumLock]);
        Assert.Equal(Path.Combine(paths[4], "brightness"), selected[LockKey.ScrollLock]);
    }

    [Fact]
    public void SelectLedPathsReturnsEmptyForMissingLeds()
    {
        Assert.Empty(SysfsLockKeySupport.SelectLedPaths(Array.Empty<string>()));
    }

    [Fact]
    public void StateTrackerEmitsOnlyTransitions()
    {
        var tracker = new SysfsLockKeyStateTracker();
        tracker.SetInitialState(LockKey.CapsLock, false);

        Assert.True(tracker.HasChanged(LockKey.CapsLock, true));
        Assert.False(tracker.HasChanged(LockKey.CapsLock, true));
        Assert.True(tracker.HasChanged(LockKey.CapsLock, false));
    }
}
