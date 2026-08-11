using System.Text.Json;
using WhatKey.Models;

namespace WhatKey.Services;

public sealed class JsonSettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };
    private readonly string _filePath;

    public JsonSettingsService(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WhatKey",
            "settings.json");
    }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_filePath))
                return new AppSettings();

            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_filePath), SerializerOptions);
            return Normalize(settings ?? new AppSettings());
        }
        catch (IOException)
        {
            return new AppSettings();
        }
        catch (JsonException)
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(_filePath, JsonSerializer.Serialize(Normalize(settings), SerializerOptions));
    }

    private static AppSettings Normalize(AppSettings settings)
    {
        settings.OverlayScale = Math.Clamp(settings.OverlayScale, 0.75, 2.0);
        if (!Enum.IsDefined(settings.OverlayPosition))
            settings.OverlayPosition = OverlayPosition.TopCenter;
        return settings;
    }
}
