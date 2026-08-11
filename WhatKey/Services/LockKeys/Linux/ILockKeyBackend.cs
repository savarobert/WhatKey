namespace WhatKey.Services;

internal interface ILockKeyBackend : IDisposable
{
    event EventHandler<LockKeyChangedEventArgs>? StateChanged;
    bool TryStart();
}
