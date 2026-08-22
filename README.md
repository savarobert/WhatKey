# WhatKey

WhatKey is a tray-only Avalonia utility that shows the resulting state of Caps Lock, Num Lock, and Scroll Lock.

## Build and publish

The project targets .NET 10 and supports Windows x64, Linux x64, macOS Apple Silicon, and macOS Intel:

```bash
dotnet publish WhatKey/WhatKey.csproj -c Release -r win-x64
dotnet publish WhatKey/WhatKey.csproj -c Release -r linux-x64
dotnet publish WhatKey/WhatKey.csproj -c Release -r osx-arm64
dotnet publish WhatKey/WhatKey.csproj -c Release -r osx-x64
```

## Linux

### Running WhatKey

WhatKey supports Linux on both Wayland and X11 where the current platform backends are available. Download and extract the published `linux-x64` archive, then run the `WhatKey` executable. A traditional installer is not required.

The Linux distribution is portable and keeps its configuration and diagnostics beside the executable:

```text
WhatKey/
├── WhatKey
├── appsettings.json
├── settings.json
└── logs/
```

### GNOME

GNOME does not display AppIndicator or StatusNotifierItem tray icons by default. WhatKey uses Avalonia's tray-icon implementation, so GNOME users should install the **AppIndicator and KStatusNotifierItem Support** GNOME Shell extension. It provides access to the WhatKey tray icon, Settings, Enable / Disable, and Exit.

#### Fedora

Install the extension package:

```bash
sudo dnf install gnome-shell-extension-appindicator
```

After installing it, log out of GNOME and log back in. This is especially important on Wayland: restarting GNOME Shell with `Alt+F2`, then `r`, is not the recommended solution there. A full logout/login reliably reloads newly installed system-wide GNOME Shell extensions.

Verify that the extension is installed:

```bash
gnome-extensions list | grep appindicator
```

The expected extension ID is:

```text
appindicatorsupport@rgcjonas.gmail.com
```

If necessary, enable it manually and check its state:

```bash
gnome-extensions enable appindicatorsupport@rgcjonas.gmail.com
gnome-extensions info appindicatorsupport@rgcjonas.gmail.com
```

Once the extension is active, the WhatKey tray icon should appear in the GNOME top bar.

### Other desktop environments

Some Linux desktop environments, including KDE Plasma, support StatusNotifierItem/AppIndicator tray icons natively and may not require an additional extension. Tray behavior depends on the desktop environment and its panel configuration.

### Lock-key monitoring

WhatKey prefers a non-privileged backend whenever one is available.

On X11, WhatKey uses the XInput2 raw-key event extension when `libX11.so.6` and `libXi.so.6` are available. If XInput2 cannot be used, it falls back to the evdev backend.

On Wayland, arbitrary global keyboard hooks are intentionally restricted by the protocol. WhatKey first attempts to read lock-key LED state from `/sys/class/leds/`, which normally requires no special permissions. If sysfs does not expose usable LEDs, it may fall back to an evdev input-device backend using `/dev/input/event*`.

Access to evdev devices can be restricted by the Linux distribution. These permissions are relevant only when the sysfs backend is unavailable; most users should not need to change input-device permissions. Raw `/dev/input` keyboard access also has security implications, so broad permission changes should be treated as an advanced troubleshooting step rather than the default setup.

If the display server, native libraries, or input-device permissions do not allow monitoring, WhatKey logs the reason and continues running in the tray with lock-key monitoring disabled. The rest of the application remains available.

### Troubleshooting

#### Tray icon missing on GNOME

Check whether the AppIndicator extension is installed and enabled:

```bash
gnome-extensions list | grep appindicator
gnome-extensions info appindicatorsupport@rgcjonas.gmail.com
```

If the extension was just installed, log out of GNOME and log back in before checking again.

#### Check the session type

```bash
echo $XDG_SESSION_TYPE
```

Typical output is `wayland` or `x11`.

#### Check or stop WhatKey

Check whether WhatKey is still running:

```bash
pgrep -a WhatKey
```

If the tray icon is unavailable, stop it with the normal termination signal:

```bash
pkill WhatKey
```

`pkill -9` is not recommended for normal shutdown.

#### Logs

Linux diagnostics are stored in `logs/` next to the application executable. Include the relevant log files when reporting a problem.

## macOS

WhatKey runs as a menu-bar/tray-only application without a main window. macOS may require permission under **System Settings > Privacy & Security > Accessibility** for global Caps Lock monitoring. WhatKey continues running without crashing if that permission is unavailable.

The current macOS release artifacts are unsigned. macOS may therefore show a Gatekeeper warning when the application is opened; signing and notarization are not included yet.

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
