# Keyboard Tracker

Windows keyboard and mouse activity tracker with real-time heatmap visualization.

Track your typing and clicking habits — which keys you press most, how far your mouse travels, and when you're most active.

## Features

- **Global input capture** — tracks keyboard and mouse across all apps via low-level Windows hooks
- **Keyboard heatmap** — visual keyboard layout with color-coded frequency (light blue → deep blue)
- **Mouse statistics** — left/right click counts and travel distance in meters
- **Activity timeline** — line chart showing keyboard + mouse activity by hour, day, or month
- **System tray** — runs quietly in background, accessible via tray icon
- **Persistent storage** — SQLite database, data survives across sessions

## Requirements

- Windows 10 or later
- .NET 9 SDK (for building from source)

## Quick Start

```bash
# Clone
git clone https://github.com/YOUR_USER/keyboard-tracker.git
cd keyboard-tracker

# Run
dotnet run

# Publish single-file exe
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish
```

## Tech Stack

| Technology | Purpose |
|---|---|
| C# .NET 9 + WPF | GUI framework |
| SQLite (Microsoft.Data.Sqlite) | Persistent storage |
| LiveCharts2 | Activity chart |
| Win32 WH_KEYBOARD_LL / WH_MOUSE_LL | Global input hooks |
| Windows Forms NotifyIcon | System tray |

## How It Works

1. **KeyboardHookService** / **MouseHookService** spawn dedicated threads with `SetWindowsHookEx` for global input capture
2. Events flow through `Channel<T>` to **StatsProcessor**
3. StatsProcessor aggregates per hour/minute in memory, flushes to SQLite every 5s
4. WPF dashboard queries SQLite, renders via data binding
5. **LiveCharts2** renders activity timeline with smooth animations
6. **NotifyIcon** provides system tray with context menu

## License

MIT
