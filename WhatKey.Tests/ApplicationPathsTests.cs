using WhatKey.Services;
using Xunit;

namespace WhatKey.Tests;

public sealed class ApplicationPathsTests
{
    [Fact]
    public void LogPathUsesThePerUserApplicationDataDirectory()
    {
        Assert.EndsWith(Path.Combine(ApplicationPaths.ApplicationName, "logs"), ApplicationPaths.LogDirectory);
        Assert.EndsWith("whatkey-.log", ApplicationPaths.LogFilePath);
    }
}
