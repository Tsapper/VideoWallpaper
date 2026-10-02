# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

- Build: `dotnet build`
- Run: `dotnet run` (tray app; stop it via the tray menu "Exit" before rebuilding, since it locks `bin/`)
- Publish: `dotnet publish -c Release` (framework-dependent single-file win-x64)
- There are no tests or linters.

## Architecture

WinForms tray app (net10.0-windows) that plays video via LibVLCSharp in a window reparented behind the desktop icons. Settings and `yt-dlp.exe` live in `%AppData%\VideoWallpaper\`.

Flow: `Program` (single-instance mutex) → `TrayApp` (ApplicationContext; owns everything) → `WallpaperWindow` + `PlayerService`. `SettingsForm` is created on demand and calls back `TrayApp.ApplySettings`, which re-shows the window on the chosen monitor and calls `PlayerService.Start`.

- `DesktopHost`: P/Invoke that sends Progman message `0x052C` to spawn WorkerW, then `SetParent`s the form into it. Handles both the Win11 24H2 layout (WorkerW is a child of Progman) and the older top-level WorkerW layout. `WallpaperWindow` re-attaches on the `TaskbarCreated` message (Explorer restart) and re-fits on display changes.
- `PlayerService`: single LibVLC instance and `MediaPlayer`. Local files loop via `:input-repeat`; YouTube items are resolved to direct stream URLs by `YtDlpService` (video + audio passed via `:input-slave`), and `EndReached` advances/reshuffles the queue. A single video is just a one-item queue. Stream URLs expire, so errors re-resolve the current entry.
- `YtDlpService`: downloads yt-dlp on first use, self-updates once on failure. Format selection uses `-S "res:<screenHeight>,fps:30,vcodec:h264"` — resolution must take priority; an earlier `[fps<=30]` filter silently dropped most videos to 360p because high-res YouTube is often 60 fps only.
- Video fills the screen via `CropGeometry = "W:H"` (set on the `Playing` event) to cover the window rather than letterbox.

## Gotchas

- LibVLC events fire on a native thread; calling Stop/Play directly from them deadlocks. Always dispatch via `Task.Run` (see `PlayerService` constructor).
- `--no-disable-screensaver` is required so VLC does not hold a display wake request and block sleep (check with `powercfg /requests`). `TrayApp` pauses on suspend and calls `PlayerService.Restart()` on resume.
- RAM is a design goal (~100 MB target): small caching/prefetch options in `PlayerService`, workstation non-concurrent GC, and a periodic GC + `EmptyWorkingSet` in `TrayApp.TrimMemory`. Keep new features light.
- libVLC cannot hard-cap frame rate; local files play at their native fps, YouTube prefers ≤30 fps only among equal resolutions.
- DPI mode is set via the `ApplicationHighDpiMode` csproj property (not a manifest) so window size equals native pixels.
- Commit locally as changes are made; the user pushes to GitHub themselves.
