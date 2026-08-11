using System.Diagnostics;
using System.Runtime.InteropServices;
using WhatKey.Models;

namespace WhatKey.Services;

public sealed class WindowsLockKeyMonitor : ILockKeyMonitor
{
    private const int WhKeyboardLl = 13;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyUp = 0x0105;
    private const int VkCapital = 0x14;
    private const int VkNumLock = 0x90;
    private const int VkScroll = 0x91;

    private readonly LowLevelKeyboardProc _hookCallback;
    private nint _hookHandle;
    private bool _started;

    public WindowsLockKeyMonitor()
    {
        _hookCallback = HookCallback;
    }

    public event EventHandler<LockKeyChangedEventArgs>? StateChanged;

    public void Start()
    {
        if (_started || !OperatingSystem.IsWindows())
            return;

        _started = true;
        using var process = Process.GetCurrentProcess();
        using var module = process.MainModule;
        _hookHandle = SetWindowsHookEx(WhKeyboardLl, _hookCallback,
            GetModuleHandle(module?.ModuleName), 0);
    }

    public void Dispose()
    {
        if (_hookHandle != 0)
        {
            UnhookWindowsHookEx(_hookHandle);
            _hookHandle = 0;
        }

        _started = false;
        GC.SuppressFinalize(this);
    }

    private nint HookCallback(int code, nint wParam, nint lParam)
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
                StateChanged?.Invoke(this, new LockKeyChangedEventArgs(key.Value, isOn));
            }
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

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetWindowsHookEx(int idHook, LowLevelKeyboardProc callback, nint moduleHandle, uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(nint hookHandle);

    [DllImport("user32.dll")]
    private static extern nint CallNextHookEx(nint hookHandle, int code, nint wParam, nint lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint GetModuleHandle(string? moduleName);

    [DllImport("user32.dll")]
    private static extern short GetKeyState(uint keyCode);
}
