namespace WhatKey.Models;

public enum LockKey
{
    CapsLock,
    NumLock,
    ScrollLock,
}

public static class LockKeyExtensions
{
    public static string ToDisplayName(this LockKey key) => key switch
    {
        LockKey.CapsLock => "Caps Lock",
        LockKey.NumLock => "Num Lock",
        LockKey.ScrollLock => "Scroll Lock",
        _ => key.ToString(),
    };
}
