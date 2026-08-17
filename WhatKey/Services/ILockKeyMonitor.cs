using WhatKey.Models;

namespace WhatKey.Services;

public sealed class LockKeyChangedEventArgs(LockKey key, bool isOn) : EventArgs
{
    public LockKey Key { get; } = key;
    public bool IsOn { get; } = isOn;
}

public interface ILockKeyMonitor : IDisposable
{
    IObservable<LockKeyChangedEventArgs> StateChanges { get; }
    void Start();
}
