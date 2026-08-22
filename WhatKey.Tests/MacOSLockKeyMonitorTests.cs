using WhatKey.Models;
using WhatKey.Services;
using Xunit;

namespace WhatKey.Tests;

public sealed class MacOSLockKeyMonitorTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CapsLockFlagsChangedEventIsTranslated(bool isOn)
    {
        var flags = isOn ? 1UL << 16 : 0UL;

        var translated = MacOSLockKeyMonitor.TryTranslateLockKeyEvent(
            eventType: 12,
            keyCode: 57,
            flags,
            out var key,
            out var state);

        Assert.True(translated);
        Assert.Equal(LockKey.CapsLock, key);
        Assert.Equal(isOn, state);
    }

    [Theory]
    [InlineData(12, 56)]
    [InlineData(10, 57)]
    [InlineData(1, 57)]
    public void UnrelatedMacOSEventsAreIgnored(int eventType, long keyCode)
    {
        var translated = MacOSLockKeyMonitor.TryTranslateLockKeyEvent(
            eventType,
            keyCode,
            flags: 1UL << 16,
            out _,
            out _);

        Assert.False(translated);
    }
}
