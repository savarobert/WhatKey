using ReactiveUI;
using ReactiveUI.Primitives;
using WhatKey.Models;
using WhatKey.Services;

namespace WhatKey.ViewModels;

public sealed class OverlayPositionOption(OverlayPosition value, string displayName)
{
    public OverlayPosition Value { get; } = value;
    public string DisplayName { get; } = displayName;
}

public sealed class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
    private readonly AppSettings _settings;
    private OverlayPositionOption _selectedPosition;
    private double _scale;

    public SettingsViewModel(ISettingsService settingsService, AppSettings settings)
    {
        _settingsService = settingsService;
        _settings = settings;
        _selectedPosition = Positions.First(p => p.Value == settings.OverlayPosition);
        _scale = settings.OverlayScale;
        CloseCommand = ReactiveCommand.Create(() => { });
    }

    public event Action? SettingsChanged;

    public IReadOnlyList<OverlayPositionOption> Positions { get; } =
        Enum.GetValues<OverlayPosition>()
            .Select(position => new OverlayPositionOption(position, position.ToDisplayName()))
            .ToArray();

    public OverlayPositionOption SelectedPosition
    {
        get => _selectedPosition;
        set
        {
            if (_selectedPosition != value)
            {
                this.RaiseAndSetIfChanged(ref _selectedPosition, value);
                _settings.OverlayPosition = value.Value;
                Save();
            }
        }
    }

    public double Scale
    {
        get => _scale;
        set
        {
            var normalized = Math.Round(Math.Clamp(value, 0.75, 2.0), 2);
            if (_scale != normalized)
            {
                this.RaiseAndSetIfChanged(ref _scale, normalized);
                _settings.OverlayScale = normalized;
                this.RaisePropertyChanged(nameof(ScaleText));
                Save();
            }
        }
    }

    public string ScaleText => $"{Scale:P0}";
    public ReactiveCommand<RxVoid, RxVoid> CloseCommand { get; }

    public void Save()
    {
        _settingsService.Save(_settings);
        SettingsChanged?.Invoke();
    }
}
