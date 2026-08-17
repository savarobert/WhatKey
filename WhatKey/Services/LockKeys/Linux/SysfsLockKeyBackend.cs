using System.Globalization;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using WhatKey.Models;

namespace WhatKey.Services;

/// <summary>
/// Reads keyboard LED state exposed by the Linux kernel through sysfs.
/// This is useful on Wayland because it does not require a global keyboard hook
/// or read access to /dev/input.
/// </summary>
internal sealed class SysfsLockKeyBackend : ILockKeyBackend
{
    internal const string SysfsLedsPath = "/sys/class/leds";

    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(75);

    private readonly ILogger _logger;
    private readonly string _sysfsPath;
    private readonly Func<string, IEnumerable<string>> _enumerateDirectories;
    private readonly Func<string, bool> _directoryExists;
    private readonly Func<string, string> _readAllText;
    private readonly Subject<LockKeyChangedEventArgs> _stateChanges = new();
    private readonly SysfsLockKeyStateTracker _stateTracker = new();
    private readonly Dictionary<LockKey, string> _brightnessPaths = new();
    private Thread? _pollThread;
    private volatile bool _stopping;
    private bool _disposed;

    internal SysfsLockKeyBackend(
        ILogger? logger = null,
        string sysfsPath = SysfsLedsPath,
        Func<string, IEnumerable<string>>? enumerateDirectories = null,
        Func<string, bool>? directoryExists = null,
        Func<string, string>? readAllText = null)
    {
        _logger = logger ?? NullLogger.Instance;
        _sysfsPath = sysfsPath;
        _enumerateDirectories = enumerateDirectories ?? Directory.EnumerateDirectories;
        _directoryExists = directoryExists ?? Directory.Exists;
        _readAllText = readAllText ?? File.ReadAllText;
    }

    public IObservable<LockKeyChangedEventArgs> StateChanges => _stateChanges.AsObservable();

    public bool TryStart()
    {
        if (_disposed || !OperatingSystem.IsLinux() || !_directoryExists(_sysfsPath))
        {
            _logger.LogDebug("Linux sysfs LED directory {SysfsPath} is unavailable", _sysfsPath);
            return false;
        }

        string[] ledDirectories;
        try
        {
            ledDirectories = _enumerateDirectories(_sysfsPath).ToArray();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogDebug(exception, "Unable to enumerate Linux sysfs LED directory {SysfsPath}", _sysfsPath);
            return false;
        }

        _brightnessPaths.Clear();
        foreach (var (key, brightnessPath) in SysfsLockKeySupport.SelectLedPaths(ledDirectories))
        {
            try
            {
                if (!SysfsLockKeySupport.TryParseBrightness(_readAllText(brightnessPath), out var isOn))
                    continue;

                _brightnessPaths[key] = brightnessPath;
                _stateTracker.SetInitialState(key, isOn);
                _logger.LogDebug("Found Linux sysfs {LockKey} LED at {BrightnessPath}", key, brightnessPath);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                _logger.LogDebug(exception, "Unable to read Linux sysfs {LockKey} LED at {BrightnessPath}", key, brightnessPath);
            }
        }

        if (_brightnessPaths.Count == 0)
        {
            _logger.LogDebug("No usable Linux sysfs lock-key LEDs were found");
            return false;
        }

        _stopping = false;
        _pollThread = new Thread(PollLoop)
        {
            IsBackground = true,
            Name = "WhatKey Linux sysfs lock-key monitor",
        };
        _pollThread.Start();
        _logger.LogInformation("Using Linux sysfs lock-key backend for {LockKeyCount} lock keys", _brightnessPaths.Count);
        return true;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _stopping = true;
        if (_pollThread is { IsAlive: true } thread)
            thread.Join(TimeSpan.FromMilliseconds(500));

        _pollThread = null;
        _brightnessPaths.Clear();
        _stateChanges.OnCompleted();
        _stateChanges.Dispose();
        GC.SuppressFinalize(this);
    }

    private void PollLoop()
    {
        var unavailableKeys = new HashSet<LockKey>();

        try
        {
            while (!_stopping)
            {
                foreach (var (key, brightnessPath) in _brightnessPaths)
                {
                    try
                    {
                        if (!SysfsLockKeySupport.TryParseBrightness(_readAllText(brightnessPath), out var isOn))
                            continue;

                        unavailableKeys.Remove(key);
                        if (_stateTracker.HasChanged(key, isOn))
                            _stateChanges.OnNext(new LockKeyChangedEventArgs(key, isOn));
                    }
                    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                    {
                        if (unavailableKeys.Add(key))
                        {
                            _logger.LogWarning(exception,
                                "Linux sysfs {LockKey} LED became unavailable at {BrightnessPath}", key, brightnessPath);
                        }
                    }
                }

                Thread.Sleep(PollInterval);
            }
        }
        catch (ThreadInterruptedException)
        {
        }
        catch (Exception exception) when (!_stopping)
        {
            _logger.LogError(exception, "Linux sysfs lock-key monitor failed");
        }
    }
}

internal static class SysfsLockKeySupport
{
    internal static bool TryParseLedName(string? ledName, out LockKey key)
    {
        key = default;
        if (string.IsNullOrWhiteSpace(ledName))
            return false;

        var suffix = ledName.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault()?.ToLowerInvariant();
        key = suffix switch
        {
            "capslock" => LockKey.CapsLock,
            "numlock" => LockKey.NumLock,
            "scrolllock" => LockKey.ScrollLock,
            _ => default,
        };
        return suffix is "capslock" or "numlock" or "scrolllock";
    }

    internal static bool TryParseBrightness(string? value, out bool isOn)
    {
        isOn = false;
        if (!int.TryParse(value?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var brightness)
            || brightness < 0)
        {
            return false;
        }

        isOn = brightness != 0;
        return true;
    }

    internal static IReadOnlyDictionary<LockKey, string> SelectLedPaths(IEnumerable<string> ledDirectories)
    {
        var selected = new Dictionary<LockKey, string>();

        // Multiple LED aliases can exist for one key. Sort first and keep one stable
        // path per key so a single physical state change cannot produce duplicates.
        foreach (var directory in ledDirectories.OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var directoryName = Path.GetFileName(directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (TryParseLedName(directoryName, out var key) && !selected.ContainsKey(key))
                selected[key] = Path.Combine(directory, "brightness");
        }

        return selected;
    }
}

internal sealed class SysfsLockKeyStateTracker
{
    private readonly Dictionary<LockKey, bool> _states = new();

    internal void SetInitialState(LockKey key, bool isOn) => _states[key] = isOn;

    internal bool HasChanged(LockKey key, bool isOn)
    {
        if (_states.TryGetValue(key, out var previous) && previous == isOn)
            return false;

        _states[key] = isOn;
        return true;
    }
}
