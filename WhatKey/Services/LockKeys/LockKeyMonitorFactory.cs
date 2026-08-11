using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace WhatKey.Services;

public static class LockKeyMonitorFactory
{
    public static ILockKeyMonitor Create(ILoggerFactory? loggerFactory = null)
    {
        var logger = loggerFactory?.CreateLogger("WhatKey.LockKeyMonitorFactory") ?? NullLogger.Instance;

        if (OperatingSystem.IsWindows())
        {
            logger.LogInformation("Selected lock-key backend {Backend}", nameof(WindowsLockKeyMonitor));
            return new WindowsLockKeyMonitor(loggerFactory?.CreateLogger<WindowsLockKeyMonitor>());
        }

        if (OperatingSystem.IsLinux())
        {
            logger.LogInformation("Selected lock-key backend {Backend}", nameof(LinuxLockKeyMonitor));
            return new LinuxLockKeyMonitor(loggerFactory);
        }

        logger.LogWarning("Global lock-key monitoring is unavailable on this operating system");
        return new UnsupportedLockKeyMonitor();
    }
}
