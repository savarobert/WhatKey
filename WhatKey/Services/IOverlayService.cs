using WhatKey.Models;

namespace WhatKey.Services;

public interface IOverlayService : IDisposable
{
    void ApplySettings(AppSettings settings);
    void Show(LockKey key, bool isOn);
}
