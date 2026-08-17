namespace WhatKey.Models;

public sealed class AppSettings
{
    public const int DefaultOverlayDurationMs = 1300;
    public const int MinOverlayDurationMs = 100;
    public const int MaxOverlayDurationMs = 5000;

    public bool Enabled { get; set; } = true;
    public OverlayPosition OverlayPosition { get; set; } = OverlayPosition.TopCenter;
    public double OverlayScale { get; set; } = 1.0;

    private int _overlayDurationMs = DefaultOverlayDurationMs;

    public int OverlayDurationMs
    {
        get => _overlayDurationMs;
        set => _overlayDurationMs = NormalizeOverlayDuration(value);
    }

    public static int NormalizeOverlayDuration(int durationMs)
        => durationMs is <= 0 or > MaxOverlayDurationMs
            ? DefaultOverlayDurationMs
            : durationMs;
}
