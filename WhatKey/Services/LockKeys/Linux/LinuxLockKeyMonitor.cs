using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using WhatKey.Models;

namespace WhatKey.Services;

public sealed class LinuxLockKeyMonitor : ILockKeyMonitor
{
    private ILockKeyBackend? _backend;
    private IDisposable? _backendSubscription;
    private readonly ILogger<LinuxLockKeyMonitor> _logger;
    private readonly Subject<LockKeyChangedEventArgs> _stateChanges = new();
    private bool _started;
    private bool _disposed;

    public LinuxLockKeyMonitor(ILoggerFactory? loggerFactory = null)
    {
        _logger = loggerFactory?.CreateLogger<LinuxLockKeyMonitor>() ?? NullLogger<LinuxLockKeyMonitor>.Instance;
        _logger.LogInformation("Linux lock-key monitor initializing");
    }

    public IObservable<LockKeyChangedEventArgs> StateChanges => _stateChanges.AsObservable();

    public void Start()
    {
        if (_started || _disposed || !OperatingSystem.IsLinux())
            return;

        _started = true;
        foreach (var candidate in LinuxLockKeyBackendFactory.CreateCandidates(_logger))
        {
            try
            {
                _backendSubscription = candidate.StateChanges.Subscribe(OnBackendStateChanged);
                if (candidate.TryStart())
                {
                    _backend = candidate;
                    _logger.LogInformation("Using Linux lock-key backend {Backend}", candidate.GetType().Name);
                    return;
                }

                _backendSubscription.Dispose();
                _backendSubscription = null;
                candidate.Dispose();
            }
            catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException or IOException)
            {
                _logger.LogWarning(exception, "Linux lock-key backend {Backend} is unavailable", candidate.GetType().Name);
                _backendSubscription?.Dispose();
                _backendSubscription = null;
                candidate.Dispose();
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Linux lock-key backend {Backend} failed", candidate.GetType().Name);
                _backendSubscription?.Dispose();
                _backendSubscription = null;
                candidate.Dispose();
            }
        }

        _logger.LogWarning("No usable Linux global lock-key backend was found; monitoring is disabled");
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        if (_backend is not null)
        {
            _backendSubscription?.Dispose();
            _backendSubscription = null;
            _backend.Dispose();
            _backend = null;
        }

        _logger.LogInformation("Linux lock-key monitor disposed");
        _started = false;
        _stateChanges.OnCompleted();
        _stateChanges.Dispose();
        GC.SuppressFinalize(this);
    }

    private void OnBackendStateChanged(LockKeyChangedEventArgs e) => _stateChanges.OnNext(e);
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
