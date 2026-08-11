using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using WhatKey.Services;

namespace WhatKey;

public partial class App : Avalonia.Application
{
    private TrayApplicationController? _controller;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _controller = new TrayApplicationController(desktop);
            _controller.Start();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
