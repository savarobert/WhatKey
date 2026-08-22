using WhatKey.Services;
using Xunit;

namespace WhatKey.Tests;

public sealed class LogSanitizationTests
{
    [Fact]
    public void SettingsPathLogSanitizationEscapesCarriageReturnAndLineFeed()
    {
        var value = JsonSettingsService.SanitizeForLog("settings\r\nforged-entry");

        Assert.Equal("settings\\r\\nforged-entry", value);
    }

    [Theory]
    [InlineData("wayland", "wayland")]
    [InlineData("WAYLAND", "wayland")]
    [InlineData(" x11 ", "x11")]
    [InlineData(null, "unknown")]
    [InlineData("", "unknown")]
    [InlineData("desktop", "unknown")]
    [InlineData("wayland\r\ninjected", "unknown")]
    public void SessionTypeLogValueIsAllowListed(string? sessionType, string expected)
    {
        Assert.Equal(expected, LinuxLockKeyBackendFactory.NormalizeSessionTypeForLog(sessionType));
    }
}
