namespace WhatKey.Services;

internal interface ILockKeyBackend : IDisposable
{
    IObservable<LockKeyChangedEventArgs> StateChanges { get; }
    bool TryStart();
}
