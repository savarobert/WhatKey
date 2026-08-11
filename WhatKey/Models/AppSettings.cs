namespace WhatKey.Models;

public sealed class AppSettings
{
    public bool Enabled { get; set; } = true;
    public OverlayPosition OverlayPosition { get; set; } = OverlayPosition.TopCenter;
    public double OverlayScale { get; set; } = 1.0;
}
