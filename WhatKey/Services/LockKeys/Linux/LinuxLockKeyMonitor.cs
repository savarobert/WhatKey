using System.Diagnostics;
using WhatKey.Models;

namespace WhatKey.Services;

public sealed class LinuxLockKeyMonitor : ILockKeyMonitor
{
    private ILockKeyBackend? _backend;
    private bool _started;

    public event EventHandler<LockKeyChangedEventArgs>? StateChanged;

    public void Start()
    {
        if (_started || !OperatingSystem.IsLinux())
            return;

        _started = true;
        foreach (var candidate in LinuxLockKeyBackendFactory.CreateCandidates())
        {
            try
            {
                candidate.StateChanged += OnBackendStateChanged;
                if (candidate.TryStart())
                {
                    _backend = candidate;
                    Trace.WriteLine($"WhatKey: using Linux lock-key backend {candidate.GetType().Name}.");
                    return;
                }

                candidate.StateChanged -= OnBackendStateChanged;
                candidate.Dispose();
            }
            catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException or IOException)
            {
                Trace.WriteLine($"WhatKey: Linux lock-key backend {candidate.GetType().Name} is unavailable: {exception.Message}");
                candidate.StateChanged -= OnBackendStateChanged;
                candidate.Dispose();
            }
            catch (Exception exception)
            {
                Trace.WriteLine($"WhatKey: Linux lock-key backend {candidate.GetType().Name} failed: {exception.Message}");
                candidate.StateChanged -= OnBackendStateChanged;
                candidate.Dispose();
            }
        }

        Trace.WriteLine("WhatKey: no usable Linux global lock-key backend was found. Monitoring is disabled.");
    }

    public void Dispose()
    {
        if (_backend is not null)
        {
            _backend.StateChanged -= OnBackendStateChanged;
            _backend.Dispose();
            _backend = null;
        }

        _started = false;
        GC.SuppressFinalize(this);
    }

    private void OnBackendStateChanged(object? sender, LockKeyChangedEventArgs e) => StateChanged?.Invoke(this, e);
}

internal static class LinuxLockKeyBackendFactory
{
    public static IEnumerable<ILockKeyBackend> CreateCandidates()
    {
        var sessionType = Environment.GetEnvironmentVariable("XDG_SESSION_TYPE")?.Trim().ToLowerInvariant();
        var isWayland = sessionType == "wayland" ||
                        (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")) && sessionType != "x11");
        var isX11 = sessionType == "x11" ||
                    (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DISPLAY")) && !isWayland);

        if (isX11)
        {
            yield return new X11LockKeyBackend();
            yield return new LinuxEvdevLockKeyBackend();
            yield break;
        }

        if (isWayland)
        {
            yield return new WaylandEvdevLockKeyBackend();
            yield break;
        }

        // Headless sessions and unusual compositors can still use evdev when permissions allow it.
        yield return new LinuxEvdevLockKeyBackend();
    }
}
