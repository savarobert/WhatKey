using WhatKey.Services;
using Xunit;

namespace WhatKey.Tests;

public sealed class ApplicationPathsTests
{
    [Fact]
    public void PathsUseTheApplicationDirectory()
    {
        Assert.Equal(AppContext.BaseDirectory, ApplicationPaths.BaseDirectory);
        Assert.Equal(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), ApplicationPaths.AppSettingsPath);
        Assert.Equal(Path.Combine(AppContext.BaseDirectory, "settings.json"), ApplicationPaths.UserSettingsPath);
        Assert.Equal(Path.Combine(AppContext.BaseDirectory, "logs"), ApplicationPaths.LogsDirectory);
        Assert.EndsWith("whatkey-.log", ApplicationPaths.LogFilePath);
    }
}
