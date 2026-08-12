using System.Runtime.InteropServices;
using Avalonia.Controls;
using Microsoft.Extensions.Logging;

namespace WhatKey.Services;

internal interface IWindowTopmostService
{
    void EnsureTopmost(Window window);
}

internal static class WindowTopmostServiceFactory
{
    public static IWindowTopmostService Create(ILogger logger)
        => OperatingSystem.IsWindows()
            ? new WindowsWindowTopmostService(logger)
            : new NoOpWindowTopmostService();
}

internal sealed class NoOpWindowTopmostService : IWindowTopmostService
{
    public void EnsureTopmost(Window window)
    {
    }
}

internal sealed class WindowsWindowTopmostService(ILogger logger) : IWindowTopmostService
{
    private static readonly nint HwndTopmost = new(-1);

    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoActivate = 0x0010;

    public void EnsureTopmost(Window window)
    {
        var platformHandle = window.TryGetPlatformHandle();
        if (platformHandle is null)
        {
            logger.LogDebug("Overlay native handle is not available for topmost reassertion");
            return;
        }

        if (!string.Equals(platformHandle.HandleDescriptor, "HWND", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogDebug(
                "Skipping Windows topmost reassertion because the platform handle is {HandleDescriptor}",
                platformHandle.HandleDescriptor);
            return;
        }

        var flags = SwpNoMove | SwpNoSize | SwpNoActivate;
        if (SetWindowPos(platformHandle.Handle, HwndTopmost, 0, 0, 0, 0, flags))
        {
            logger.LogDebug("Reasserted overlay topmost state");
            return;
        }

        logger.LogWarning(
            "Failed to reassert overlay topmost state with SetWindowPos; Win32 error {ErrorCode}",
            Marshal.GetLastWin32Error());
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        nint hWnd,
        nint hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint flags);
}
