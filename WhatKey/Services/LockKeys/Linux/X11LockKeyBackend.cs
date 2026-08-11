using System.Diagnostics;
using System.Runtime.InteropServices;
using WhatKey.Models;

namespace WhatKey.Services;

internal sealed class X11LockKeyBackend : ILockKeyBackend
{
    private const int GenericEvent = 35;
    private const int XiRawKeyPress = 13;
    private const int XiRawKeyRelease = 14;
    private const int XiAllMasterDevices = 1;
    private const int XkbUseCoreKbd = 0x100;
    private const ulong CapsLockKeysym = 0xFFE5;
    private const ulong NumLockKeysym = 0xFF7F;
    private const ulong ScrollLockKeysym = 0xFF14;

    private readonly Dictionary<LockKey, bool> _states = new();
    private readonly HashSet<LockKey> _pressedKeys = new();
    private nint _display;
    private int _extensionOpcode;
    private Thread? _eventThread;
    private volatile bool _stopping;

    public event EventHandler<LockKeyChangedEventArgs>? StateChanged;

    public bool TryStart()
    {
        if (!OperatingSystem.IsLinux())
            return false;

        try
        {
            _display = XOpenDisplay(null);
            if (_display == 0)
                return false;

            if (XQueryExtension(_display, "XInputExtension", out _extensionOpcode, out _, out _) == 0)
                return Fail("XInputExtension is unavailable");

            var major = 2;
            var minor = 0;
            if (XIQueryVersion(_display, ref major, ref minor) != 0)
                return Fail("XInput2 is unavailable");

            var mask = Marshal.AllocHGlobal(2);
            try
            {
                Marshal.WriteByte(mask, 0, 0);
                Marshal.WriteByte(mask, 1, (byte)((1 << (XiRawKeyPress % 8)) | (1 << (XiRawKeyRelease % 8))));
                var eventMask = new XIEventMask
                {
                    DeviceId = XiAllMasterDevices,
                    MaskLength = 2,
                    Mask = mask,
                };
                var root = XRootWindow(_display, XDefaultScreen(_display));
                if (XISelectEvents(_display, root, ref eventMask, 1) != 0)
                    return Fail("unable to select XInput2 raw keyboard events");
            }
            finally
            {
                Marshal.FreeHGlobal(mask);
            }

            InitializeState();
            XFlush(_display);
            _stopping = false;
            _eventThread = new Thread(EventLoop)
            {
                IsBackground = true,
                Name = "WhatKey X11 input monitor",
            };
            _eventThread.Start();
            return true;
        }
        catch (DllNotFoundException exception)
        {
            Trace.WriteLine($"WhatKey: X11 libraries are unavailable: {exception.Message}");
        }
        catch (EntryPointNotFoundException exception)
        {
            Trace.WriteLine($"WhatKey: X11 input entry point is unavailable: {exception.Message}");
        }

        Dispose();
        return false;
    }

    public void Dispose()
    {
        _stopping = true;
        if (_eventThread is { IsAlive: true } thread)
            thread.Join(TimeSpan.FromMilliseconds(500));

        _eventThread = null;
        if (_display != 0)
        {
            XCloseDisplay(_display);
            _display = 0;
        }
    }

    private bool Fail(string message)
    {
        Trace.WriteLine($"WhatKey: {message}.");
        Dispose();
        return false;
    }

    private void InitializeState()
    {
        var state = new XkbStateRec();
        if (XkbGetState(_display, XkbUseCoreKbd, ref state) == 0)
        {
            _states[LockKey.CapsLock] = (state.LockedMods & 0x02) != 0;
            // These are the conventional XKB modifier assignments; subsequent events update exactly.
            _states[LockKey.NumLock] = (state.LockedMods & 0x10) != 0;
            _states[LockKey.ScrollLock] = (state.LockedMods & 0x20) != 0;
        }
    }

    private void EventLoop()
    {
        try
        {
            while (!_stopping && _display != 0)
            {
                if (XPending(_display) == 0)
                {
                    Thread.Sleep(10);
                    continue;
                }

                XNextEvent(_display, out var xEvent);
                var cookie = xEvent.Generic;
                if (cookie.Type != GenericEvent || cookie.Extension != _extensionOpcode ||
                    cookie.EvType is not (XiRawKeyPress or XiRawKeyRelease) || XGetEventData(_display, ref cookie) == 0)
                    continue;

                try
                {
                    if (cookie.Data == 0)
                        continue;

                    var rawEvent = Marshal.PtrToStructure<XIRawEvent>(cookie.Data);
                    var keysym = XkbKeycodeToKeysym(_display, (byte)rawEvent.Detail, 0, 0);
                    if (!TryMapKeysym(keysym, out var key))
                        continue;

                    if (cookie.EvType == XiRawKeyRelease)
                    {
                        _pressedKeys.Remove(key);
                        continue;
                    }

                    if (!_pressedKeys.Add(key))
                        continue;

                    var isOn = !_states.GetValueOrDefault(key);
                    _states[key] = isOn;
                    StateChanged?.Invoke(this, new LockKeyChangedEventArgs(key, isOn));
                }
                finally
                {
                    XFreeEventData(_display, ref cookie);
                }
            }
        }
        catch (Exception exception) when (!_stopping)
        {
            Trace.WriteLine($"WhatKey: X11 input monitor failed: {exception.Message}");
        }
    }

    private static bool TryMapKeysym(ulong keysym, out LockKey key)
    {
        key = keysym switch
        {
            CapsLockKeysym => LockKey.CapsLock,
            NumLockKeysym => LockKey.NumLock,
            ScrollLockKeysym => LockKey.ScrollLock,
            _ => default,
        };
        return keysym is CapsLockKeysym or NumLockKeysym or ScrollLockKeysym;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XIEventMask
    {
        public int DeviceId;
        public int MaskLength;
        public nint Mask;
    }

    [StructLayout(LayoutKind.Explicit, Size = 192)]
    private struct XEvent
    {
        [FieldOffset(0)] public XGenericEventCookie Generic;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XGenericEventCookie
    {
        public int Type;
        public int Padding;
        public ulong Serial;
        public int SendEvent;
        public nint Display;
        public int Extension;
        public int EvType;
        public uint Cookie;
        public nint Data;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XIRawEvent
    {
        public int Type;
        public int Padding;
        public ulong Serial;
        public int SendEvent;
        public int Padding2;
        public nint Display;
        public int Extension;
        public int EvType;
        public int DeviceId;
        public int SourceId;
        public int Detail;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XkbStateRec
    {
        public byte Group;
        public byte LockedGroup;
        public ushort BaseGroup;
        public ushort LatchedGroup;
        public byte Mods;
        public byte BaseMods;
        public byte LatchedMods;
        public byte LockedMods;
        public byte CompatState;
        public byte GrabMods;
        public byte CompatGrabMods;
        public byte LookupMods;
        public byte CompatLookupMods;
        public ushort PtrButtons;
    }

    [DllImport("libX11.so.6", EntryPoint = "XOpenDisplay")] private static extern nint XOpenDisplay(string? displayName);
    [DllImport("libX11.so.6", EntryPoint = "XCloseDisplay")] private static extern int XCloseDisplay(nint display);
    [DllImport("libX11.so.6", EntryPoint = "XDefaultScreen")] private static extern int XDefaultScreen(nint display);
    [DllImport("libX11.so.6", EntryPoint = "XRootWindow")] private static extern nint XRootWindow(nint display, int screen);
    [DllImport("libX11.so.6", EntryPoint = "XQueryExtension")] private static extern int XQueryExtension(nint display, string name, out int opcode, out int firstEvent, out int firstError);
    [DllImport("libX11.so.6", EntryPoint = "XFlush")] private static extern int XFlush(nint display);
    [DllImport("libX11.so.6", EntryPoint = "XPending")] private static extern int XPending(nint display);
    [DllImport("libX11.so.6", EntryPoint = "XNextEvent")] private static extern int XNextEvent(nint display, out XEvent xEvent);
    [DllImport("libX11.so.6", EntryPoint = "XGetEventData")] private static extern int XGetEventData(nint display, ref XGenericEventCookie cookie);
    [DllImport("libX11.so.6", EntryPoint = "XFreeEventData")] private static extern void XFreeEventData(nint display, ref XGenericEventCookie cookie);
    [DllImport("libX11.so.6", EntryPoint = "XkbGetState")] private static extern int XkbGetState(nint display, uint deviceSpec, ref XkbStateRec state);
    [DllImport("libX11.so.6", EntryPoint = "XkbKeycodeToKeysym")] private static extern ulong XkbKeycodeToKeysym(nint display, byte keycode, int group, int level);
    [DllImport("libXi.so.6", EntryPoint = "XIQueryVersion")] private static extern int XIQueryVersion(nint display, ref int major, ref int minor);
    [DllImport("libXi.so.6", EntryPoint = "XISelectEvents")] private static extern int XISelectEvents(nint display, nint window, ref XIEventMask masks, int count);
}
