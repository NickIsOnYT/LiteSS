# LiteSS

A very small Windows screenshot tool written in C# (WinForms, .NET 8). Plain dark UI, no
gradients, glows, or shadows.

## Features

- **Capture Region** — dims the desktop and lets you drag a rectangle (all monitors).
- **Full Screen** — captures the primary monitor.
- **Copy** — puts the screenshot on the clipboard.
- **Save** — saves as PNG.
- **System tray** — closing or minimizing the window hides LiteSS to the tray and keeps it
  running. Double-click the tray icon to reopen it; right-click for capture, settings and exit.
- **Settings** — opened from the Settings button (or the tray menu).

## Settings

The Settings window contains:

- **After capture** — copy to clipboard, save automatically to a folder, and show notifications.
- **Startup** — launch at Windows startup, and optionally start silently in the tray (`--tray`).
- **Shortcut** — register PrintScreen as a global shortcut for a region capture.

Settings are saved as JSON in:

```
%LOCALAPPDATA%\LiteSS\settings.json
```

## Shortcuts

| Shortcut | Action |
| --- | --- |
| `Ctrl+N` | Capture a region |
| `Ctrl+S` | Save as PNG |
| `Ctrl+C` | Copy to clipboard |
| `PrintScreen` | Capture a region (after registering it in Settings) |
| `Esc` / right-click | Cancel a region capture |

## Requirements

- Windows
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

## Build and run

```powershell
dotnet run
```

To produce a release build:

```powershell
dotnet publish -c Release
```

If you enable **Launch at startup**, the registered command points at the executable that is
currently running. Launch the built exe (or a published copy) when you enable it so Windows
starts that copy at sign-in.
