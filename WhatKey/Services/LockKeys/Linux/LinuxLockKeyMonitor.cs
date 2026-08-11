using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using WhatKey.Models;

namespace WhatKey.Services;

public sealed class LinuxLockKeyMonitor : ILockKeyMonitor
{
    private ILockKeyBackend? _backend;
    private readonly ILogger<LinuxLockKeyMonitor> _logger;
    private bool _started;

    public LinuxLockKeyMonitor(ILoggerFactory? loggerFactory = null)
    {
        _logger = loggerFactory?.CreateLogger<LinuxLockKeyMonitor>() ?? NullLogger<LinuxLockKeyMonitor>.Instance;
        _logger.LogInformation("Linux lock-key monitor initializing");
    }

    public event EventHandler<LockKeyChangedEventArgs>? StateChanged;

    public void Start()
    {
        if (_started || !OperatingSystem.IsLinux())
            return;

        _started = true;
        foreach (var candidate in LinuxLockKeyBackendFactory.CreateCandidates(_logger))
        {
            try
            {
                candidate.StateChanged += OnBackendStateChanged;
                if (candidate.TryStart())
                {
                    _backend = candidate;
                    _logger.LogInformation("Using Linux lock-key backend {Backend}", candidate.GetType().Name);
                    return;
                }

                candidate.StateChanged -= OnBackendStateChanged;
                candidate.Dispose();
            }
            catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException or IOException)
            {
                _logger.LogWarning(exception, "Linux lock-key backend {Backend} is unavailable", candidate.GetType().Name);
                candidate.StateChanged -= OnBackendStateChanged;
                candidate.Dispose();
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Linux lock-key backend {Backend} failed", candidate.GetType().Name);
                candidate.StateChanged -= OnBackendStateChanged;
                candidate.Dispose();
            }
        }

        _logger.LogWarning("No usable Linux global lock-key backend was found; monitoring is disabled");
    }

    public void Dispose()
    {
        if (_backend is not null)
        {
            _backend.StateChanged -= OnBackendStateChanged;
            _backend.Dispose();
            _backend = null;
        }

        _logger.LogInformation("Linux lock-key monitor disposed");
        _started = false;
        GC.SuppressFinalize(this);
    }

    private void OnBackendStateChanged(object? sender, LockKeyChangedEventArgs e) => StateChanged?.Invoke(this, e);
}

internal static class LinuxLockKeyBackendFactory
{
    public static IEnumerable<ILockKeyBackend> CreateCandidates(ILogger logger)
    {
        var sessionType = Environment.GetEnvironmentVariable("XDG_SESSION_TYPE")?.Trim().ToLowerInvariant();
        logger.LogInformation("Detected Linux session type {SessionType}", sessionType ?? "unknown");
        var isWayland = sessionType == "wayland" ||
                        (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")) && sessionType != "x11");
        var isX11 = sessionType == "x11" ||
                    (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DISPLAY")) && !isWayland);

        if (isX11)
        {
            logger.LogDebug("Probing X11 XInput2 lock-key backend");
            yield return new X11LockKeyBackend(logger);
            logger.LogDebug("Probing Linux evdev lock-key fallback");
            yield return new LinuxEvdevLockKeyBackend(logger);
            yield break;
        }

        if (isWayland)
        {
            logger.LogInformation("Detected Wayland; using evdev-compatible global input backend");
            yield return new WaylandEvdevLockKeyBackend(logger);
            yield break;
        }

        // Headless sessions and unusual compositors can still use evdev when permissions allow it.
        logger.LogDebug("Probing Linux evdev lock-key backend for an unknown session");
        yield return new LinuxEvdevLockKeyBackend(logger);
    }
}
