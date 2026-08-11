namespace WhatKey.Models;

public enum OverlayPosition
{
    TopLeft,
    TopCenter,
    TopRight,
    CenterLeft,
    Center,
    CenterRight,
    BottomLeft,
    BottomCenter,
    BottomRight,
}

public static class OverlayPositionExtensions
{
    public static string ToDisplayName(this OverlayPosition position) => position switch
    {
        OverlayPosition.TopLeft => "Top Left",
        OverlayPosition.TopCenter => "Top Center",
        OverlayPosition.TopRight => "Top Right",
        OverlayPosition.CenterLeft => "Center Left",
        OverlayPosition.Center => "Center",
        OverlayPosition.CenterRight => "Center Right",
        OverlayPosition.BottomLeft => "Bottom Left",
        OverlayPosition.BottomCenter => "Bottom Center",
        OverlayPosition.BottomRight => "Bottom Right",
        _ => position.ToString(),
    };
}
