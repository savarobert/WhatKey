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

On Wayland, arbitrary global keyboard hooks are intentionally restricted by the protocol. WhatKey therefore uses the evdev input-device backend. The user must be able to read `/dev/input/event*`; on many distributions this means adding the user to the `input` group and starting a new login session. No root privileges are required to launch the application when that permission is already granted.

If the display server, native libraries, or input-device permissions do not allow monitoring, WhatKey logs the reason and continues running in the tray with only lock-key monitoring disabled. The rest of the application remains available.

## Logging

WhatKey uses Serilog through the standard `Microsoft.Extensions.Logging` abstractions. Logs are written to daily rolling files with a 14-file retention limit and a 10 MB size limit per file.

- Windows: `%LOCALAPPDATA%/WhatKey/logs/`
- Linux: `~/.local/share/WhatKey/logs/`

Logs include timestamps, levels, source context, structured diagnostic values, and exception details. They can be included when submitting bug reports.
