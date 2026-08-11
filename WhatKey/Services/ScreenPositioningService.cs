using System.Runtime.InteropServices;
using WhatKey.Models;

namespace WhatKey.Services;

public sealed class ScreenPositioningService
{
    private const uint MonitorDefaultToNearest = 2;

    public ScreenArea GetActiveWorkArea()
    {
        if (OperatingSystem.IsWindows() && GetCursorPos(out var cursor))
        {
            var monitor = MonitorFromPoint(cursor, MonitorDefaultToNearest);
            if (monitor != 0 && TryGetMonitorInfo(monitor, out var info))
            {
                return new ScreenArea(
                    info.Work.Left,
                    info.Work.Top,
                    info.Work.Right - info.Work.Left,
                    info.Work.Bottom - info.Work.Top);
            }
        }

        return new ScreenArea(0, 0, 1920, 1080);
    }

    public OverlayCoordinates CalculatePosition(ScreenArea workArea, int width, int height,
        OverlayPosition position) => OverlayPositionCalculator.Calculate(workArea, width, height, position);

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rectangle
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public Rectangle Monitor;
        public Rectangle Work;
        public uint Flags;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out Point point);

    [DllImport("user32.dll")]
    private static extern nint MonitorFromPoint(Point point, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);

    private static bool TryGetMonitorInfo(nint monitor, out MonitorInfo info)
    {
        info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        return GetMonitorInfo(monitor, ref info);
    }
}
