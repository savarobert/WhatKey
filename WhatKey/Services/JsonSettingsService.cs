using System.Text.Json;
using Microsoft.Extensions.Logging;
using WhatKey.Models;

namespace WhatKey.Services;

public sealed class JsonSettingsService : ISettingsService
{
    private readonly string _filePath;
    private readonly ILogger<JsonSettingsService>? _logger;

    public JsonSettingsService(string? filePath = null, ILogger<JsonSettingsService>? logger = null)
    {
        _filePath = filePath ?? ApplicationPaths.UserSettingsPath;
        _logger = logger;
    }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                var defaults = new AppSettings();
                _logger?.LogInformation("Settings file not found; creating default settings file");
                if (TrySave(defaults))
                    _logger?.LogInformation("Default settings file created");
                return defaults;
            }

            var settings = JsonSerializer.Deserialize(
                File.ReadAllText(_filePath),
                AppSettingsJsonContext.Default.AppSettings);
            var normalized = Normalize(settings ?? new AppSettings());
            _logger?.LogInformation("Settings loaded from {SettingsPath}", _filePath);
            return normalized;
        }
        catch (JsonException exception)
        {
            _logger?.LogWarning(exception, "Settings file is invalid; using defaults");
            return new AppSettings();
        }
        catch (IOException exception)
        {
            _logger?.LogError(exception, "Failed to load settings from {SettingsPath}; using defaults", _filePath);
            return new AppSettings();
        }
        catch (UnauthorizedAccessException exception)
        {
            _logger?.LogError(exception, "Access to settings file {SettingsPath} was denied; using defaults", _filePath);
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
        => TrySave(settings);

    private bool TrySave(AppSettings settings)
    {
        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(
                _filePath,
                JsonSerializer.Serialize(Normalize(settings), AppSettingsJsonContext.Default.AppSettings));
            _logger?.LogDebug("Settings saved to {SettingsPath}", _filePath);
            return true;
        }
        catch (Exception exception)
        {
            _logger?.LogError(exception, "Failed to save settings to {SettingsPath}", _filePath);
            return false;
        }
    }

    private static AppSettings Normalize(AppSettings settings)
    {
        settings.OverlayScale = Math.Clamp(settings.OverlayScale, 0.75, 2.0);
        if (!Enum.IsDefined(settings.OverlayPosition))
            settings.OverlayPosition = OverlayPosition.TopCenter;
        settings.OverlayDurationMs = AppSettings.NormalizeOverlayDuration(settings.OverlayDurationMs);
        return settings;
    }
}
