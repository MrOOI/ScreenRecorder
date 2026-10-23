# Test report — v1.0.0

Date: 2026-10-23. Machine: Windows 10 Pro 22H2 (build 19045), two 1920×1080 displays at 100 % scaling, .NET SDK 10.0.101 building `net8.0-windows`.

## Automated

| Check | Result |
|---|---|
| `dotnet build` (warnings as errors) | 0 warnings, 0 errors |
| `dotnet test` | 105 passed, 0 failed |
| `dotnet format --verify-no-changes` | clean |
| Build and test from a fresh `git clone` | passed |
| Line coverage, `ScreenRecorder.Core` | 97.8 % (target ≥ 85 %) |
| Line coverage, view models | 86–100 % per class |

Not unit-tested by design: `ScreenRecorderLibEngine`, Win32 interop, WPF views and window services. They are covered by the manual runs below.

## Manual matrix (PLAN.md §6.2)

Runs were driven by UI Automation and injected input against the real app, and the output files were checked with Windows shell metadata.

| # | Scenario | Result | Evidence |
|---|---|---|---|
| 1 | Full screen, system audio + mic, 30 fps | **Pass (partial)** | 1920×1080 MP4, about 29–30 fps constant, AAC track present (about 180 kbps). Audio/video sync was not judged by ear. Playback was checked via Windows metadata only, not VLC. |
| 2 | Secondary display | **Pass** | Log shows `\\.\DISPLAY2`, output 1920×1080. |
| 3 | Region on a 150 % DPI display | **Not run** | No high-DPI display available. A region at 100 % produced an exact 640×360 file from a 641×361 drag, trimmed to even size. |
| 4 | Window source, window moved mid-recording | **Pass (partial)** | Chrome window recorded at its own size (1920×1040). Moving the window during recording was not exercised. |
| 5 | Pause, then resume | **Pass** | 2 s pause via hotkey; file length excludes it. A unit test covers elapsed-time accounting. |
| 6 | Hotkeys with another app focused | **Pass** | Start, pause, resume and stop were sent while the editor had focus. |
| 7 | Close main window while recording | **Not run manually** | The window is hidden during recording, so it cannot be closed then. Tray **Exit** stops and saves first (unit-tested). |
| 8 | Output folder on a missing drive | **Pass** | Inline error "Can't save to …" appears; no countdown starts and the app keeps running. |
| 9 | Second instance | **Pass** | The second process exits and the first stays running. |
| 10 | Corrupt settings file | **Pass** | Starts with defaults, logs a warning, keeps the bad file as `settings.json.corrupt`. |

## Defects found and fixed during testing

- Source drop-down showed the record's debug text. Fixed by passing the item template selector through the custom ComboBox template.
- "System audio" checkbox was clipped. Fixed by widening the window and using a wrapping row.
- Shortcut fields used the stock text box style, because implicit styles skip subclasses. Fixed with an explicit style.
- Single-file publish failed to load the capture library (`BadImageFormatException`). Fixed by keeping `ScreenRecorderLib.dll` beside the exe.
- With the capture library unloadable, a cleanup error during exit showed a dialog and kept the process alive. Fixed: shutdown errors are logged, never shown.
- The macOS `*.app` rule in `.gitignore` silently excluded the WPF project folder from git. Fixed with an explicit exception.

## Still to verify before a wider release

- Scenario 3 on a 125–200 % display, including mixed-DPI multi-monitor setups.
- Scenario 1 audio sync over a recording of several minutes.
- Windows 11 run, including the borderless window capture.
- Installer: install, launch, uninstall on a clean machine. It compiles, but it has not been executed.
