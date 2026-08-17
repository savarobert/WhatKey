using WhatKey.Models;

namespace WhatKey.Services;

public interface IOverlayService : IDisposable
{
    void ApplySettings(AppSettings settings);
    IDisposable Bind(IObservable<LockKeyChangedEventArgs> updates);
}
