using Avalonia.Controls;
using Avalonia.Controls.Templates;
using WhatKey.ViewModels;
using WhatKey.Views;

namespace WhatKey;

/// <summary>
/// Given a view model, returns the corresponding view if possible.
/// </summary>
public sealed class ViewLocator : IDataTemplate
{
    public Control? Build(object? param)
    {
        return param switch
        {
            OverlayViewModel => new OverlayWindow(),
            SettingsViewModel => new SettingsWindow(),
            ViewModelBase => new TextBlock { Text = "Not Found" },
            _ => null
        };
    }

    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}
