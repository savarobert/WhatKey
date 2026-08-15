namespace WhatKey.Services;

public static class ApplicationPaths
{
    public const string ApplicationName = "WhatKey";

    public static string BaseDirectory => AppContext.BaseDirectory;

    public static string AppSettingsPath => Path.Combine(BaseDirectory, "appsettings.json");
    public static string UserSettingsPath => Path.Combine(BaseDirectory, "settings.json");
    public static string LogsDirectory => Path.Combine(BaseDirectory, "logs");
    public static string LogFilePath => Path.Combine(LogsDirectory, "whatkey-.log");

    // Keep the existing names available to callers while the path concepts are clarified.
    public static string SettingsFilePath => UserSettingsPath;
    public static string LogDirectory => LogsDirectory;
}
