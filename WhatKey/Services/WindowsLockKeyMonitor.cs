using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using WhatKey.Models;

namespace WhatKey.Services;

public sealed partial class WindowsLockKeyMonitor : ILockKeyMonitor
{
    private const int WhKeyboardLl = 13;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyUp = 0x0105;
    private const int VkCapital = 0x14;
    private const int VkNumLock = 0x90;
    private const int VkScroll = 0x91;

    private readonly LowLevelKeyboardProc _hookCallback;
    private readonly ILogger<WindowsLockKeyMonitor> _logger;
    private nint _hookHandle;
    private bool _started;

    public WindowsLockKeyMonitor(ILogger<WindowsLockKeyMonitor>? logger = null)
    {
        _hookCallback = HookCallback;
        _logger = logger ?? NullLogger<WindowsLockKeyMonitor>.Instance;
    }

    public event EventHandler<LockKeyChangedEventArgs>? StateChanged;

    public void Start()
    {
        if (_started || !OperatingSystem.IsWindows())
            return;

        _logger.LogInformation("Initializing Windows low-level lock-key monitor");
        _started = true;
        using var process = System.Diagnostics.Process.GetCurrentProcess();
        using var module = process.MainModule;
        _hookHandle = SetWindowsHookEx(WhKeyboardLl, _hookCallback,
            GetModuleHandle(module?.ModuleName), 0);
        if (_hookHandle == 0)
        {
            var errorCode = Marshal.GetLastWin32Error();
            _logger.LogError("Failed to install Windows keyboard hook. Win32Error: {Win32Error}; Message: {NativeMessage}",
                errorCode, new Win32Exception(errorCode).Message);
            _started = false;
            return;
        }

        _logger.LogInformation("Windows low-level keyboard hook installed");
    }

    public void Dispose()
    {
        if (_hookHandle != 0)
        {
            var hookHandle = _hookHandle;
            if (!UnhookWindowsHookEx(hookHandle))
            {
                var errorCode = Marshal.GetLastWin32Error();
                _logger.LogWarning("Failed to release Windows keyboard hook. Win32Error: {Win32Error}; Message: {NativeMessage}",
                    errorCode, new Win32Exception(errorCode).Message);
            }
            else
            {
                _logger.LogInformation("Windows low-level keyboard hook released");
            }

            _hookHandle = 0;
        }

        _started = false;
        GC.SuppressFinalize(this);
    }

    private nint HookCallback(int code, nint wParam, nint lParam)
    {
        try
        {
            if (code >= 0 && (wParam == WmKeyUp || wParam == WmSysKeyUp))
            {
                var hookData = Marshal.PtrToStructure<KbdLlHookStruct>(lParam);
                var key = hookData.VkCode switch
                {
                    VkCapital => LockKey.CapsLock,
                    VkNumLock => LockKey.NumLock,
                    VkScroll => LockKey.ScrollLock,
                    _ => (LockKey?)null,
                };

                if (key.HasValue)
                {
                    var isOn = (GetKeyState(hookData.VkCode) & 1) != 0;
                    _logger.LogDebug("{LockKey} state changed to {State}", key.Value, isOn ? "ON" : "OFF");
                    StateChanged?.Invoke(this, new LockKeyChangedEventArgs(key.Value, isOn));
                }
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected Windows keyboard hook callback failure");
        }

        return CallNextHookEx(_hookHandle, code, wParam, lParam);
    }

    private delegate nint LowLevelKeyboardProc(int code, nint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct KbdLlHookStruct
    {
        public uint VkCode;
        public uint ScanCode;
        public uint Flags;
        public uint Time;
        public nint DwExtraInfo;
    }

    [LibraryImport("user32.dll", SetLastError = true)]
    private  static partial nint SetWindowsHookEx(int idHook, LowLevelKeyboardProc callback, nint moduleHandle, uint threadId);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UnhookWindowsHookEx(nint hookHandle);

    [LibraryImport("user32.dll")]
    private static partial nint CallNextHookEx(nint hookHandle, int code, nint wParam, nint lParam);

    [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    private static partial nint GetModuleHandle(string? moduleName);

    [LibraryImport("user32.dll")]
    private static partial short GetKeyState(uint keyCode);
}
