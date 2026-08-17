using System.Runtime.InteropServices;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Microsoft.Win32.SafeHandles;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using WhatKey.Models;

namespace WhatKey.Services;

internal class LinuxEvdevLockKeyBackend : ILockKeyBackend
{
    private const ushort EvKey = 0x01;
    private const ushort EvLed = 0x11;
    private const ushort KeyCapsLock = 58;
    private const ushort KeyNumLock = 69;
    private const ushort KeyScrollLock = 70;
    private const ushort LedNumLock = 0;
    private const ushort LedCapsLock = 1;
    private const ushort LedScrollLock = 2;
    private const int KeyBitsLength = 96;
    private const int EventSize64 = 24;
    private const int EventSize32 = 16;

    private readonly Dictionary<LockKey, bool> _states = new();
    private readonly ILogger _logger;
    private readonly Subject<LockKeyChangedEventArgs> _stateChanges = new();
    private FileStream? _stream;
    private Thread? _readerThread;
    private volatile bool _stopping;
    private bool _disposed;

    internal LinuxEvdevLockKeyBackend(ILogger? logger = null)
    {
        _logger = logger ?? NullLogger.Instance;
    }

    public IObservable<LockKeyChangedEventArgs> StateChanges => _stateChanges.AsObservable();

    public bool TryStart()
    {
        if (_disposed || !OperatingSystem.IsLinux())
            return false;

        try
        {
            var permissionDeniedCount = 0;
            foreach (var path in Directory.EnumerateFiles("/dev/input", "event*"))
            {
                FileStream? stream = null;
                try
                {
                    stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                        FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.SequentialScan);

                    if (!HasLockKeyCapability(stream.SafeFileHandle))
                    {
                        stream.Dispose();
                        continue;
                    }

                    _stream = stream;
                    _logger.LogInformation("Opened Linux input device {DevicePath}", path);
                    LoadInitialLedState(stream.SafeFileHandle);
                    _stopping = false;
                    _readerThread = new Thread(ReadLoop)
                    {
                        IsBackground = true,
                        Name = "WhatKey Linux input monitor",
                    };
                    _readerThread.Start();
                    return true;
                }
                catch (UnauthorizedAccessException)
                {
                    stream?.Dispose();
                    permissionDeniedCount++;
                    _logger.LogDebug("Permission denied reading Linux input device {DevicePath}", path);
                }
                catch (IOException)
                {
                    stream?.Dispose();
                }
            }

            if (permissionDeniedCount > 0)
            {
                _logger.LogWarning("No usable evdev keyboard could be opened; access was denied for {DeniedDeviceCount} Linux input devices", permissionDeniedCount);
                return false;
            }
        }
        catch (DirectoryNotFoundException)
        {
            _logger.LogWarning("/dev/input is unavailable; Linux global monitoring is disabled");
        }
        catch (UnauthorizedAccessException)
        {
            _logger.LogWarning("Permission denied enumerating /dev/input; evdev monitoring is unavailable");
            return false;
        }

        _logger.LogWarning("No Linux input device exposing Caps Lock, Num Lock, or Scroll Lock was found");
        return false;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _stopping = true;
        _stream?.Dispose();
        if (_readerThread is { IsAlive: true } thread)
            thread.Join(TimeSpan.FromMilliseconds(500));

        _readerThread = null;
        _stream = null;
        _stateChanges.OnCompleted();
        _stateChanges.Dispose();
        GC.SuppressFinalize(this);
    }

    private void ReadLoop()
    {
        var stream = _stream;
        if (stream is null)
            return;

        var buffer = new byte[IntPtr.Size == 8 ? EventSize64 : EventSize32];
        var typeOffset = IntPtr.Size == 8 ? 16 : 8;

        try
        {
            while (!_stopping)
            {
                stream.ReadExactly(buffer);
                var type = BitConverter.ToUInt16(buffer, typeOffset);
                var code = BitConverter.ToUInt16(buffer, typeOffset + 2);
                var value = BitConverter.ToInt32(buffer, typeOffset + 4);
                ProcessEvent(type, code, value);
            }
        }
        catch (EndOfStreamException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        catch (IOException exception) when (_stopping)
        {
            _logger.LogDebug(exception, "Linux input monitor stopped");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Linux input monitor failed");
        }
    }

    private void ProcessEvent(ushort type, ushort code, int value)
    {
        if (type == EvKey && value == 1 && TryMapKey(code, out var key))
        {
            var isOn = !_states.GetValueOrDefault(key);
            _states[key] = isOn;
            _stateChanges.OnNext(new LockKeyChangedEventArgs(key, isOn));
        }
        else if (type == EvLed && TryMapLed(code, out var ledKey))
        {
            var isOn = value != 0;
            if (!_states.TryGetValue(ledKey, out var previous) || previous != isOn)
            {
                _states[ledKey] = isOn;
                _stateChanges.OnNext(new LockKeyChangedEventArgs(ledKey, isOn));
            }
        }
    }

    private void LoadInitialLedState(SafeFileHandle handle)
    {
        var leds = new byte[1];
        if (Ioctl(handle.DangerousGetHandle(), IoctlRead('E', 0x19, leds.Length), leds) == 0)
        {
            _states[LockKey.NumLock] = (leds[0] & (1 << LedNumLock)) != 0;
            _states[LockKey.CapsLock] = (leds[0] & (1 << LedCapsLock)) != 0;
            _states[LockKey.ScrollLock] = (leds[0] & (1 << LedScrollLock)) != 0;
            _logger.LogDebug("Loaded Linux lock-key LED state: CapsLock={CapsLock}, NumLock={NumLock}, ScrollLock={ScrollLock}",
                _states[LockKey.CapsLock], _states[LockKey.NumLock], _states[LockKey.ScrollLock]);
        }
    }

    private static bool HasLockKeyCapability(SafeFileHandle handle)
    {
        var bits = new byte[KeyBitsLength];
        if (Ioctl(handle.DangerousGetHandle(), IoctlRead('E', 0x20 + EvKey, bits.Length), bits) != 0)
            return false;

        return HasBit(bits, KeyCapsLock) || HasBit(bits, KeyNumLock) || HasBit(bits, KeyScrollLock);
    }

    private static bool HasBit(byte[] bits, int bit) => (bits[bit / 8] & (1 << (bit % 8))) != 0;

    private static bool TryMapKey(ushort code, out LockKey key)
    {
        key = code switch
        {
            KeyCapsLock => LockKey.CapsLock,
            KeyNumLock => LockKey.NumLock,
            KeyScrollLock => LockKey.ScrollLock,
            _ => default,
        };
        return code is KeyCapsLock or KeyNumLock or KeyScrollLock;
    }

    private static bool TryMapLed(ushort code, out LockKey key)
    {
        key = code switch
        {
            LedCapsLock => LockKey.CapsLock,
            LedNumLock => LockKey.NumLock,
            LedScrollLock => LockKey.ScrollLock,
            _ => default,
        };
        return code is LedCapsLock or LedNumLock or LedScrollLock;
    }

    private static ulong IoctlRead(char type, int number, int size) =>
        (2UL << 30) | ((ulong)size << 16) | ((ulong)type << 8) | (uint)number;

    [DllImport("libc", EntryPoint = "ioctl", SetLastError = true)]
    private static extern int Ioctl(nint fileDescriptor, ulong request, [Out] byte[] data);
}

internal sealed class WaylandEvdevLockKeyBackend : LinuxEvdevLockKeyBackend
{
    public WaylandEvdevLockKeyBackend(ILogger? logger = null) : base(logger)
    {
    }
}
