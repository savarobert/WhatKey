using System.Diagnostics;

namespace WhatKey.Services;

public static class LockKeyMonitorFactory
{
    public static ILockKeyMonitor Create()
    {
        if (OperatingSystem.IsWindows())
            return new WindowsLockKeyMonitor();

        if (OperatingSystem.IsLinux())
            return new LinuxLockKeyMonitor();

        Trace.WriteLine("WhatKey: global lock-key monitoring is unavailable on this operating system.");
        return new UnsupportedLockKeyMonitor();
    }
}
