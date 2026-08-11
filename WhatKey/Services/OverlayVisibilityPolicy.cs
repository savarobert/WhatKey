using WhatKey.Models;

namespace WhatKey.Services;

public static class OverlayVisibilityPolicy
{
    public static bool ShouldShow(AppSettings settings) => settings.Enabled;
}
