# WhatKey

WhatKey is a tray-only Avalonia utility that shows the resulting state of Caps Lock, Num Lock, and Scroll Lock.

## Build and publish

The project targets .NET 10 and supports both Windows and Linux:

```bash
dotnet publish WhatKey/WhatKey.csproj -c Release -r win-x64
dotnet publish WhatKey/WhatKey.csproj -c Release -r linux-x64
```

## Linux global keyboard monitoring

On X11, WhatKey uses the XInput2 raw-key event extension when `libX11.so.6` and `libXi.so.6` are available. If XInput2 cannot be used, it falls back to the evdev backend.

On Wayland, arbitrary global keyboard hooks are intentionally restricted by the protocol. WhatKey first uses the lock-key LED state exposed by `/sys/class/leds`, which normally works for an unprivileged user. If the compositor or kernel does not expose usable sysfs LEDs, it falls back to the evdev input-device backend. The evdev fallback may require read access to `/dev/input/event*` (for example, through a distribution-specific device-permission rule or membership of the `input` group). No root privileges are required to launch the application.

If the display server, native libraries, or input-device permissions do not allow monitoring, WhatKey logs the reason and continues running in the tray with only lock-key monitoring disabled. The rest of the application remains available.

## Logging

WhatKey uses Serilog through the standard `Microsoft.Extensions.Logging` abstractions. The portable runtime layout beside the executable is:

```text
WhatKey/
├── WhatKey.exe / WhatKey
├── appsettings.json
├── settings.json
└── logs/
```

Logs are written to daily rolling files with the retention and size limits from `appsettings.json`. The application directory must be writable for settings persistence and local file logging.

Logs include timestamps, levels, source context, structured diagnostic values, and exception details. They can be included when submitting bug reports.
