namespace WhatKey.Services;

public static class ApplicationPaths
{
    public const string ApplicationName = "WhatKey";

    public static string DataDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        ApplicationName);

    public static string SettingsFilePath => Path.Combine(DataDirectory, "settings.json");
    public static string LogDirectory => Path.Combine(DataDirectory, "logs");
    public static string LogFilePath => Path.Combine(LogDirectory, "whatkey-.log");
}
