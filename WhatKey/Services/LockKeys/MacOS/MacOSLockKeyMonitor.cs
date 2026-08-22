using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using WhatKey.Models;

namespace WhatKey.Services;

public sealed unsafe partial class MacOSLockKeyMonitor : ILockKeyMonitor
{
    private readonly object _sync = new();
    private readonly ILogger<MacOSLockKeyMonitor> _logger;
    private readonly Subject<LockKeyChangedEventArgs> _stateChanges = new();
    private Thread? _eventThread;
    private nint _runLoop;
    private nint _eventTap;
    private nint _runLoopSource;
    private GCHandle _selfHandle;
    private bool _selfHandleAllocated;
    private bool _started;
    private bool _disposed;

    public MacOSLockKeyMonitor(ILogger<MacOSLockKeyMonitor>? logger = null)
    {
        _logger = logger ?? NullLogger<MacOSLockKeyMonitor>.Instance;
    }

    public IObservable<LockKeyChangedEventArgs> StateChanges => _stateChanges.AsObservable();

    public void Start()
    {
        lock (_sync)
        {
            if (_started || _disposed || !OperatingSystem.IsMacOS())
                return;

            _started = true;
            _selfHandle = GCHandle.Alloc(this);
            _selfHandleAllocated = true;
            _eventThread = new Thread(RunEventLoop)
            {
                IsBackground = true,
                Name = "WhatKey macOS lock-key monitor",
            };
        }

        _logger.LogInformation("Initializing macOS event tap");
        try
        {
            _eventThread!.Start();
        }
        catch (Exception exception)
        {
            lock (_sync)
            {
                _started = false;
                if (_selfHandleAllocated)
                {
                    _selfHandle.Free();
                    _selfHandleAllocated = false;
                }
            }

            _logger.LogError(exception, "Failed to start macOS event tap thread");
        }
    }

    public void Dispose()
    {
        Thread? eventThread;
        nint runLoop;

        lock (_sync)
        {
            if (_disposed)
                return;

            _disposed = true;
            eventThread = _eventThread;
            runLoop = _runLoop;
        }

        if (runLoop != 0)
        {
            try
            {
                MacOSNativeMethods.Stop(runLoop);
            }
            catch (Exception exception)
            {
                _logger.LogDebug(exception, "Failed to stop macOS event loop during shutdown");
            }
        }

        if (eventThread is { IsAlive: true } && !ReferenceEquals(Thread.CurrentThread, eventThread))
            eventThread.Join(TimeSpan.FromSeconds(2));

        _stateChanges.OnCompleted();
        _stateChanges.Dispose();
        GC.SuppressFinalize(this);
    }

    internal static bool TryTranslateLockKeyEvent(
        int eventType,
        long keyCode,
        ulong flags,
        out LockKey key,
        out bool isOn)
    {
        key = default;
        isOn = false;

        if (eventType != MacOSNativeMethods.FlagsChangedEventType
            || keyCode != MacOSNativeMethods.CapsLockKeyCode)
        {
            return false;
        }

        key = LockKey.CapsLock;
        isOn = (flags & MacOSNativeMethods.AlphaShiftFlag) != 0;
        return true;
    }

    private void RunEventLoop()
    {
        var runLoop = nint.Zero;
        var eventTap = nint.Zero;
        var runLoopSource = nint.Zero;
        var defaultMode = nint.Zero;

        try
        {
            runLoop = MacOSNativeMethods.GetCurrentRunLoop();
            lock (_sync)
                _runLoop = runLoop;

            var eventMask = 1UL << MacOSNativeMethods.FlagsChangedEventType;
            eventTap = MacOSNativeMethods.CreateEventTap(eventMask, EventTapCallbackPointer, GCHandle.ToIntPtr(_selfHandle));
            if (eventTap == 0)
            {
                _logger.LogWarning(
                    "macOS input permission unavailable; grant WhatKey access under System Settings > Privacy & Security > Accessibility");
                return;
            }

            runLoopSource = MacOSNativeMethods.CreateRunLoopSource(eventTap);
            if (runLoopSource == 0)
            {
                _logger.LogError("Unable to create the macOS event tap run-loop source");
                return;
            }

            lock (_sync)
            {
                if (_disposed)
                    return;

                _eventTap = eventTap;
                _runLoopSource = runLoopSource;
            }

            defaultMode = MacOSNativeMethods.CreateDefaultRunLoopMode();
            if (defaultMode == 0)
            {
                _logger.LogError("Unable to create the macOS default run-loop mode");
                return;
            }

            MacOSNativeMethods.AddSource(runLoop, runLoopSource, defaultMode);
            MacOSNativeMethods.EnableEventTap(eventTap, enabled: true);
            _logger.LogInformation("macOS global keyboard monitoring initialized");

            lock (_sync)
            {
                if (_disposed)
                    return;
            }

            MacOSNativeMethods.Run(runLoop);
        }
        catch (DllNotFoundException exception)
        {
            _logger.LogWarning(exception, "macOS CoreGraphics/CoreFoundation libraries are unavailable");
        }
        catch (EntryPointNotFoundException exception)
        {
            _logger.LogWarning(exception, "macOS event-tap API is unavailable");
        }
        catch (Exception exception) when (!IsDisposed())
        {
            _logger.LogError(exception, "macOS lock-key event loop failed");
        }
        finally
        {
            if (defaultMode != 0)
                MacOSNativeMethods.Release(defaultMode);

            CleanupNativeResources(runLoop, eventTap, runLoopSource);

            lock (_sync)
            {
                _runLoop = 0;
                _eventTap = 0;
                _runLoopSource = 0;
                if (_selfHandleAllocated)
                {
                    _selfHandle.Free();
                    _selfHandleAllocated = false;
                }
            }
        }
    }

    private void CleanupNativeResources(nint runLoop, nint eventTap, nint runLoopSource)
    {
        if (runLoop != 0 && runLoopSource != 0)
        {
            try
            {
                var mode = MacOSNativeMethods.CreateDefaultRunLoopMode();
                if (mode != 0)
                {
                    MacOSNativeMethods.RemoveSource(runLoop, runLoopSource, mode);
                    MacOSNativeMethods.Release(mode);
                }
            }
            catch (Exception exception)
            {
                _logger.LogDebug(exception, "Failed to remove macOS event source during shutdown");
            }
        }

        if (eventTap != 0)
        {
            try
            {
                MacOSNativeMethods.EnableEventTap(eventTap, enabled: false);
                MacOSNativeMethods.InvalidateMachPort(eventTap);
            }
            catch (Exception exception)
            {
                _logger.LogDebug(exception, "Failed to invalidate macOS event tap during shutdown");
            }

            MacOSNativeMethods.Release(eventTap);
            _logger.LogInformation("macOS event tap disposed");
        }

        if (runLoopSource != 0)
            MacOSNativeMethods.Release(runLoopSource);
    }

    private bool IsDisposed()
    {
        lock (_sync)
            return _disposed;
    }

    private void HandleEvent(nint eventRef)
    {
        try
        {
            var keyCode = MacOSNativeMethods.GetIntegerValueField(
                eventRef,
                MacOSNativeMethods.KeyboardEventKeycodeField);
            var flags = MacOSNativeMethods.GetFlags(eventRef);
            if (!TryTranslateLockKeyEvent(
                    MacOSNativeMethods.FlagsChangedEventType,
                    keyCode,
                    flags,
                    out var key,
                    out var isOn))
            {
                return;
            }

            lock (_sync)
            {
                if (_disposed)
                    return;

                _logger.LogDebug("{LockKey} state changed to {State}", key, isOn ? "ON" : "OFF");
                _stateChanges.OnNext(new LockKeyChangedEventArgs(key, isOn));
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected macOS event-tap callback failure");
        }
    }

    private void HandleDisabledEvent(nint eventTap)
    {
        try
        {
            MacOSNativeMethods.EnableEventTap(eventTap, enabled: true);
            _logger.LogWarning("macOS event tap was disabled by the system and has been re-enabled");
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Unable to re-enable the macOS event tap");
        }
    }

    private static readonly delegate* unmanaged[Cdecl]<nint, int, nint, nint, nint> EventTapCallbackPointer = &EventTapCallback;

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static nint EventTapCallback(nint proxy, int eventType, nint eventRef, nint userInfo)
    {
        try
        {
            if (userInfo == 0)
                return eventRef;

            var handle = GCHandle.FromIntPtr(userInfo);
            if (handle.Target is not MacOSLockKeyMonitor monitor)
                return eventRef;

            if (eventType is MacOSNativeMethods.TapDisabledByTimeoutEventType
                or MacOSNativeMethods.TapDisabledByUserInputEventType)
            {
                monitor.HandleDisabledEvent(proxy);
            }
            else if (eventType == MacOSNativeMethods.FlagsChangedEventType && eventRef != 0)
            {
                monitor.HandleEvent(eventRef);
            }
        }
        catch
        {
            // Native callbacks must never allow an exception to cross the ABI boundary.
        }

        return eventRef;
    }
}
