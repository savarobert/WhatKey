using WhatKey.Models;

namespace WhatKey.Services;

public static class OverlayPositionCalculator
{
    public static OverlayCoordinates Calculate(ScreenArea workArea, int overlayWidth, int overlayHeight,
        OverlayPosition position, int inset = 24)
    {
        var x = position switch
        {
            OverlayPosition.TopLeft or OverlayPosition.CenterLeft or OverlayPosition.BottomLeft => workArea.X + inset,
            OverlayPosition.TopCenter or OverlayPosition.Center or OverlayPosition.BottomCenter =>
                workArea.X + (workArea.Width - overlayWidth) / 2,
            _ => workArea.X + workArea.Width - overlayWidth - inset,
        };

        var y = position switch
        {
            OverlayPosition.TopLeft or OverlayPosition.TopCenter or OverlayPosition.TopRight => workArea.Y + inset,
            OverlayPosition.CenterLeft or OverlayPosition.Center or OverlayPosition.CenterRight =>
                workArea.Y + (workArea.Height - overlayHeight) / 2,
            _ => workArea.Y + workArea.Height - overlayHeight - inset,
        };

        return new OverlayCoordinates(x, y);
    }
}
