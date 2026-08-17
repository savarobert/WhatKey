using ReactiveUI;
using ReactiveUI.Primitives;
using System.Reactive;
using System.Reactive.Subjects;
using WhatKey.Models;
using WhatKey.Services;

namespace WhatKey.ViewModels;

public sealed class OverlayPositionOption(OverlayPosition value, string displayName)
{
    public OverlayPosition Value { get; } = value;
    public string DisplayName { get; } = displayName;
}

public sealed class SettingsViewModel : ViewModelBase, IDisposable
{
    private readonly ISettingsService _settingsService;
    private readonly AppSettings _settings;
    private OverlayPositionOption _selectedPosition;
    private double _scale;
    private double _durationMs;
    private readonly Subject<Unit> _settingsChanges = new();
    private bool _isDisposed;

    public SettingsViewModel(ISettingsService settingsService, AppSettings settings)
    {
        _settingsService = settingsService;
        _settings = settings;
        _selectedPosition = Positions.First(p => p.Value == settings.OverlayPosition);
        _scale = settings.OverlayScale;
        _durationMs = settings.OverlayDurationMs;
        CloseCommand = ReactiveCommand.Create(() => { });
    }

    public IObservable<Unit> SettingsChanges => _settingsChanges;

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

    public double DurationMs
    {
        get => _durationMs;
        set
        {
            var normalized = Math.Clamp(
                Math.Round(value / 100.0, MidpointRounding.AwayFromZero) * 100,
                AppSettings.MinOverlayDurationMs,
                AppSettings.MaxOverlayDurationMs);
            if (Math.Abs(_durationMs - normalized) > double.Epsilon)
            {
                this.RaiseAndSetIfChanged(ref _durationMs, normalized);
                _settings.OverlayDurationMs = (int)normalized;
                this.RaisePropertyChanged(nameof(DurationText));
                Save();
            }
        }
    }

    public string DurationText => $"{DurationMs / 1000.0:0.0} s";

    public ReactiveCommand<RxVoid, RxVoid> CloseCommand { get; }

    public void Save()
    {
        if (_isDisposed)
            return;

        _settingsService.Save(_settings);
        _settingsChanges.OnNext(Unit.Default);
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        _settingsChanges.OnCompleted();
        _settingsChanges.Dispose();
    }
}
