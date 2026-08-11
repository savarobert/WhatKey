using ReactiveUI;
using WhatKey.Models;

namespace WhatKey.ViewModels;

public sealed class OverlayViewModel : ViewModelBase
{
    private string _keyName = string.Empty;
    private bool _isOn;
    private double _scale = 1.0;

    public string KeyName
    {
        get => _keyName;
        private set => this.RaiseAndSetIfChanged(ref _keyName, value);
    }

    public bool IsOn
    {
        get => _isOn;
        private set
        {
            if (_isOn != value)
            {
                this.RaiseAndSetIfChanged(ref _isOn, value);
                this.RaisePropertyChanged(nameof(StateText));
            }
        }
    }

    public string StateText => IsOn ? "ON" : "OFF";

    public double Scale
    {
        get => _scale;
        set
        {
            var normalized = Math.Clamp(value, 0.75, 2.0);
            if (_scale != normalized)
            {
                this.RaiseAndSetIfChanged(ref _scale, normalized);
                this.RaisePropertyChanged(nameof(FontSize));
                this.RaisePropertyChanged(nameof(StateFontSize));
                this.RaisePropertyChanged(nameof(VerticalPadding));
            }
        }
    }

    public double FontSize => 24 * Scale;
    public double StateFontSize => 16 * Scale;
    public double VerticalPadding => 18 * Scale;

    public void Show(LockKey key, bool isOn)
    {
        KeyName = key.ToDisplayName();
        IsOn = isOn;
    }
}
