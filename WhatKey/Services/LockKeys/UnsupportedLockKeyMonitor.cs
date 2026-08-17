namespace WhatKey.Services;

public sealed class UnsupportedLockKeyMonitor : ILockKeyMonitor
{
    public IObservable<LockKeyChangedEventArgs> StateChanges =>
        System.Reactive.Linq.Observable.Empty<LockKeyChangedEventArgs>();

    public void Start()
    {
        // Unsupported platforms keep the tray application running without monitoring.
    }

    public void Dispose()
    {
    }
}
