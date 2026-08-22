using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace WhatKey.Services;

internal static unsafe partial class MacOSNativeMethods
{
    private const string CoreGraphicsLibrary = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";
    private const string CoreFoundationLibrary = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

    internal const int HidEventTap = 0;
    internal const int HeadInsertEventTap = 0;
    internal const int ListenOnlyEventTap = 1;
    internal const int FlagsChangedEventType = 12;
    internal const int TapDisabledByTimeoutEventType = unchecked((int)0xFFFFFFFE);
    internal const int TapDisabledByUserInputEventType = -1;
    internal const long KeyboardEventKeycodeField = 9;
    internal const long CapsLockKeyCode = 57;
    internal const ulong AlphaShiftFlag = 1UL << 16;
    internal const uint Utf8Encoding = 0x08000100;

    internal static nint CreateEventTap(
        ulong eventMask,
        delegate* unmanaged[Cdecl]<nint, int, nint, nint, nint> callback,
        nint userInfo)
        => CGEventTapCreate(
            HidEventTap,
            HeadInsertEventTap,
            ListenOnlyEventTap,
            eventMask,
            callback,
            userInfo);

    internal static nint CreateRunLoopSource(nint eventTap)
        => CFMachPortCreateRunLoopSource(nint.Zero, eventTap, 0);

    internal static nint GetCurrentRunLoop() => CFRunLoopGetCurrent();

    internal static nint CreateDefaultRunLoopMode()
        => CFStringCreateWithCString(nint.Zero, "kCFRunLoopDefaultMode", Utf8Encoding);

    internal static void AddSource(nint runLoop, nint source, nint mode)
        => CFRunLoopAddSource(runLoop, source, mode);

    internal static void RemoveSource(nint runLoop, nint source, nint mode)
        => CFRunLoopRemoveSource(runLoop, source, mode);

    internal static void Run(nint runLoop) => CFRunLoopRun();

    internal static void Stop(nint runLoop) => CFRunLoopStop(runLoop);

    internal static void EnableEventTap(nint eventTap, bool enabled)
        => CGEventTapEnable(eventTap, enabled ? (byte)1 : (byte)0);

    internal static long GetIntegerValueField(nint eventRef, long field)
        => CGEventGetIntegerValueField(eventRef, field);

    internal static ulong GetFlags(nint eventRef) => CGEventGetFlags(eventRef);

    internal static void InvalidateMachPort(nint eventTap) => CFMachPortInvalidate(eventTap);

    internal static void Release(nint handle) => CFRelease(handle);

    [LibraryImport(CoreGraphicsLibrary, EntryPoint = "CGEventTapCreate")]
    private static partial nint CGEventTapCreate(
        int tap,
        int place,
        int options,
        ulong eventsOfInterest,
        delegate* unmanaged[Cdecl]<nint, int, nint, nint, nint> callback,
        nint userInfo);

    [LibraryImport(CoreGraphicsLibrary, EntryPoint = "CGEventTapEnable")]
    private static partial void CGEventTapEnable(nint tap, byte enable);

    [LibraryImport(CoreGraphicsLibrary, EntryPoint = "CGEventGetIntegerValueField")]
    private static partial long CGEventGetIntegerValueField(nint eventRef, long field);

    [LibraryImport(CoreGraphicsLibrary, EntryPoint = "CGEventGetFlags")]
    private static partial ulong CGEventGetFlags(nint eventRef);

    [LibraryImport(CoreFoundationLibrary, EntryPoint = "CFMachPortCreateRunLoopSource")]
    private static partial nint CFMachPortCreateRunLoopSource(nint allocator, nint port, long order);

    [LibraryImport(CoreFoundationLibrary, EntryPoint = "CFRunLoopGetCurrent")]
    private static partial nint CFRunLoopGetCurrent();

    [LibraryImport(CoreFoundationLibrary, EntryPoint = "CFRunLoopAddSource")]
    private static partial void CFRunLoopAddSource(nint runLoop, nint source, nint mode);

    [LibraryImport(CoreFoundationLibrary, EntryPoint = "CFRunLoopRemoveSource")]
    private static partial void CFRunLoopRemoveSource(nint runLoop, nint source, nint mode);

    [LibraryImport(CoreFoundationLibrary, EntryPoint = "CFRunLoopRun")]
    private static partial void CFRunLoopRun();

    [LibraryImport(CoreFoundationLibrary, EntryPoint = "CFRunLoopStop")]
    private static partial void CFRunLoopStop(nint runLoop);

    [LibraryImport(CoreFoundationLibrary, EntryPoint = "CFStringCreateWithCString", StringMarshalling = StringMarshalling.Utf8)]
    private static partial nint CFStringCreateWithCString(nint allocator, string cStr, uint encoding);

    [LibraryImport(CoreFoundationLibrary, EntryPoint = "CFMachPortInvalidate")]
    private static partial void CFMachPortInvalidate(nint port);

    [LibraryImport(CoreFoundationLibrary, EntryPoint = "CFRelease")]
    private static partial void CFRelease(nint cf);
}
