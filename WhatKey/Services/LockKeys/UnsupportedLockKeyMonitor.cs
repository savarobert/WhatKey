namespace WhatKey.Services;

public sealed class UnsupportedLockKeyMonitor : ILockKeyMonitor
{
    public event EventHandler<LockKeyChangedEventArgs>? StateChanged
    {
        add { }
        remove { }
    }

    public void Start()
    {
        // Unsupported platforms keep the tray application running without monitoring.
    }

    public void Dispose()
    {
    }
}
