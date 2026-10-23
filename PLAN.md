# ScreenRecorder — Full-Cycle Implementation Plan

Windows desktop screen recorder. .NET 8, WPF, MVVM, Clean Architecture, SOLID.
Goal: minimal UI, one-click recording to MP4, nothing the user has to learn.

This document is the single source of truth for implementation. Work through the
phases in order. Each phase ends with a build that compiles and tests that pass.

> **Status (2026-10-23): v1.0.0 implemented.** Decisions that changed during implementation
> are recorded in [section 9](#9-implementation-record-v100); where sections 1–8 disagree, section 9 wins.

---

## 1. Product scope

### 1.1 Must have (v1.0)
- Record **full screen** (primary or chosen display), a **window**, or a **custom region**.
- Output **MP4 (H.264 + AAC)** to a user-chosen folder; default `Videos\ScreenRecorder`.
- Optional **system audio** and **microphone** capture (independent toggles).
- Optional **mouse cursor** in the recording.
- **Start / Pause / Resume / Stop** from the window, the tray icon, and **global hotkeys**.
- **3-second countdown** before recording starts (toggleable).
- Live **elapsed timer** and recording state indicator.
- Window **minimizes to tray** during recording so it is never in the recording.
- **Settings** persist between runs (JSON in `%AppData%\ScreenRecorder\settings.json`).
- After stop: toast-like notice with **Open folder** / **Open file**.
- Errors are surfaced in plain language; the app never crashes on a failed recording.

### 1.2 Nice to have (v1.1, only after v1.0 is done)
- Frame-rate and quality presets (Low / Medium / High).
- Recording history list in the main window.
- Single-file publish and an installer.

### 1.3 Out of scope
- Video editing, trimming, webcam overlay, streaming, cloud upload, cross-platform.

### 1.4 Target
- Windows 10 1903+ and Windows 11, x64.
- .NET 8 (LTS) — installed SDK: 8.0.417.

---

## 2. Key technical decisions

| Topic | Decision | Why |
|---|---|---|
| UI framework | WPF, .NET 8 | Mature, native look, MVVM-friendly, no WinUI packaging friction. |
| MVVM | `CommunityToolkit.Mvvm` (source generators) | Boilerplate-free `ObservableProperty` / `RelayCommand`, testable VMs. |
| DI / hosting | `Microsoft.Extensions.Hosting` + `DependencyInjection` | Standard composition root, options, logging. |
| Capture + encode | `ScreenRecorderLib` (NuGet, MIT) behind `IRecordingEngine` | Proven DXGI Desktop Duplication + Media Foundation H.264/AAC, audio loopback, cursor, pause/resume. Hand-rolling DXGI + MF is weeks of risk for no user-visible gain. The interface isolates it so it can be replaced later. |
| Tray icon | `H.NotifyIcon.Wpf` | Maintained WPF tray icon with context menu. |
| Global hotkeys | Win32 `RegisterHotKey` via `HwndSource` hook, wrapped in `IHotkeyService` | No dependency; ~80 lines. |
| Settings | `System.Text.Json`, `ISettingsStore` | No dependency, trivially testable. |
| Logging | `Microsoft.Extensions.Logging` + `Serilog.Extensions.Logging` + `Serilog.Sinks.File` | Rolling log in `%AppData%\ScreenRecorder\logs`. |
| Tests | xUnit + FluentAssertions + NSubstitute | ViewModels and services tested against fakes; engine is not unit-tested (integration only). |
| Platform | `PlatformTarget=x64`, `RuntimeIdentifier=win-x64` | `ScreenRecorderLib` is a C++/CLI mixed assembly and is architecture-specific. |

If `ScreenRecorderLib` cannot be restored or fails to initialize on the build machine,
fall back to: GDI `BitBlt` capture + FFmpeg (`FFMpegCore`, ffmpeg.exe piped raw BGRA frames).
This is a second `IRecordingEngine` implementation, nothing else changes.

---

## 3. Architecture

### 3.1 Solution layout

```
ScreenRecorder.sln
├─ src/
│  ├─ ScreenRecorder.Core/            # No UI, no Windows-specific deps. Pure contracts + models.
│  │  ├─ Models/
│  │  │  ├─ CaptureSource.cs          # discriminated: Display(id) | Window(handle,title) | Region(rect)
│  │  │  ├─ DisplayInfo.cs, WindowInfo.cs, ScreenRegion.cs
│  │  │  ├─ RecordingOptions.cs       # source, audio flags, cursor, fps, quality, output path
│  │  │  ├─ RecordingState.cs         # enum Idle | Countdown | Recording | Paused | Finishing | Failed
│  │  │  ├─ RecordingResult.cs        # file path, duration, size, or error
│  │  │  └─ AppSettings.cs            # persisted user preferences
│  │  ├─ Abstractions/
│  │  │  ├─ IRecordingEngine.cs       # Start/Pause/Resume/Stop + events; the only capture seam
│  │  │  ├─ IRecordingSession.cs      # orchestrates countdown, state machine, timer
│  │  │  ├─ ICaptureSourceProvider.cs # enumerates displays and windows
│  │  │  ├─ ISettingsStore.cs
│  │  │  ├─ IHotkeyService.cs
│  │  │  ├─ IOutputPathResolver.cs    # builds "Recording 2026-10-23 17-03-12.mp4" safely
│  │  │  ├─ INotificationService.cs   # "Recording saved" / "Recording failed"
│  │  │  ├─ IShellService.cs          # open folder / open file
│  │  │  └─ IClock.cs                 # testability of timers and file names
│  │  └─ Services/
│  │     ├─ RecordingSession.cs       # state machine; depends only on abstractions
│  │     └─ OutputPathResolver.cs
│  │
│  ├─ ScreenRecorder.Infrastructure/  # Windows implementations of Core abstractions
│  │  ├─ Recording/ScreenRecorderLibEngine.cs
│  │  ├─ Recording/RecordingOptionsMapper.cs   # Core options -> library options (single place)
│  │  ├─ Capture/Win32CaptureSourceProvider.cs # EnumDisplayMonitors / EnumWindows (P/Invoke)
│  │  ├─ Settings/JsonSettingsStore.cs
│  │  ├─ Hotkeys/Win32HotkeyService.cs
│  │  ├─ Shell/WindowsShellService.cs
│  │  └─ Interop/NativeMethods.cs
│  │
│  └─ ScreenRecorder.App/             # WPF, composition root, views, view models
│     ├─ App.xaml(.cs)                # builds Host, resolves MainWindow, global exception handlers
│     ├─ Views/MainWindow.xaml        # the single main window
│     ├─ Views/RegionSelectorWindow.xaml  # full-screen transparent overlay to drag a rectangle
│     ├─ Views/SettingsView.xaml      # inline expander or small dialog
│     ├─ Views/CountdownOverlay.xaml  # big number in the centre of the screen
│     ├─ ViewModels/MainViewModel.cs
│     ├─ ViewModels/SettingsViewModel.cs
│     ├─ ViewModels/RegionSelectorViewModel.cs
│     ├─ Services/TrayIconService.cs  # H.NotifyIcon, mirrors MainViewModel commands
│     ├─ Services/WpfNotificationService.cs
│     ├─ Services/WpfDispatcher.cs    # IUiDispatcher so VMs never touch Dispatcher directly
│     ├─ Converters/, Behaviors/
│     └─ Themes/Colors.xaml, Controls.xaml   # minimal design tokens
│
└─ tests/
   └─ ScreenRecorder.Tests/           # xUnit
      ├─ Core/RecordingSessionTests.cs
      ├─ Core/OutputPathResolverTests.cs
      ├─ Infrastructure/JsonSettingsStoreTests.cs
      ├─ Infrastructure/RecordingOptionsMapperTests.cs
      └─ App/MainViewModelTests.cs
```

Dependency direction: `App → Infrastructure → Core`. `Core` references nothing but the BCL.

### 3.2 Recording state machine (`RecordingSession`)

```
Idle ──Start──▶ Countdown ──tick 0──▶ Recording ──Pause──▶ Paused
  ▲                │ Cancel              │  ▲               │
  │                ▼                     │  └────Resume─────┘
  └────────────── Idle ◀── Finishing ◀──Stop
                              │
                           Failed ──Acknowledge──▶ Idle
```

- Every transition is validated; illegal transitions throw `InvalidOperationException`
  (callers guard via `CanExecute`, so the user never hits one).
- `RecordingSession` owns the elapsed timer (pauses correctly) and raises `StateChanged`
  and `ElapsedChanged`.
- Engine events are marshalled to the UI thread by `IUiDispatcher` in the App layer, never in Core.

### 3.3 SOLID mapping
- **S**: capture, encode, path building, settings, hotkeys, tray, notifications are separate classes.
- **O**: new capture source kinds or new engines are added without touching `RecordingSession`.
- **L**: any `IRecordingEngine` can be swapped; tests use `FakeRecordingEngine`.
- **I**: small interfaces (`IClock`, `IShellService`) rather than one `IPlatformService`.
- **D**: view models and session depend on abstractions; the composition root in `App.xaml.cs` wires concretes.

### 3.4 Error handling
- Engine failures → `RecordingFailed` event → state `Failed` with a user-readable message
  (`"Could not access the display. Try another source."`), details go to the log.
- `App.DispatcherUnhandledException` and `TaskScheduler.UnobservedTaskException` log and show a
  friendly dialog; the app stays alive.
- Output folder is validated and created on start; failure is reported before countdown begins.

---

## 4. UI design (minimal)

One window, ~380×260 px, fixed size, no ribbon, no tabs.

```
┌──────────────────────────────────────────────┐
│  Screen Recorder                        – ×  │
├──────────────────────────────────────────────┤
│  Source   [ Full screen ▾ ]  [ Select region ]│
│                                              │
│  ☑ System audio   ☑ Microphone   ☑ Cursor    │
│                                              │
│            ┌──────────────────┐              │
│            │   ●  Record      │   00:00:00   │
│            └──────────────────┘              │
│                                              │
│  Saves to  C:\Users\…\Videos\ScreenRecorder  ⚙│
└──────────────────────────────────────────────┘
```

- The primary button morphs: **Record** → **Stop** (with a secondary **Pause/Resume**).
- `Source` dropdown lists: Full screen (primary), each display, "Window…", "Region…".
- Settings (⚙) is a small popover: output folder, FPS (30/60), quality (Low/Med/High),
  countdown on/off, hotkeys, start minimized.
- Default hotkeys: `Ctrl+Shift+R` start/stop, `Ctrl+Shift+P` pause/resume.
- Colors: neutral grey background, one accent (red `#E5484D`) for the record state, system font.
- Light and dark follow the Windows app theme (read `AppsUseLightTheme` registry key once at start).
- Keyboard accessible; every control has an `AutomationProperties.Name`.

---

## 5. Implementation phases

Each phase: implement → `dotnet build` → `dotnet test` → short manual check where noted.

### Phase 0 — Repository and solution skeleton
1. `git init`, `.gitignore` (VisualStudio template), `.editorconfig` (C# conventions, nullable
   enabled, file-scoped namespaces, `var` preferred).
2. `Directory.Build.props`: `net8.0-windows10.0.19041.0`, `Nullable=enable`,
   `ImplicitUsings=enable`, `TreatWarningsAsErrors=true`, `LangVersion=latest`, `PlatformTarget=x64`.
3. Create projects (SDK style) and solution; add project references.
4. Add NuGet packages:
   - App: `CommunityToolkit.Mvvm`, `Microsoft.Extensions.Hosting`, `H.NotifyIcon.Wpf`,
     `Serilog.Extensions.Logging`, `Serilog.Sinks.File`.
   - Infrastructure: `ScreenRecorderLib`, `Microsoft.Extensions.Logging.Abstractions`.
   - Tests: `xunit`, `xunit.runner.visualstudio`, `FluentAssertions`, `NSubstitute`,
     `Microsoft.NET.Test.Sdk`.
5. Verify: `dotnet build` succeeds, empty WPF window runs.

### Phase 1 — Core domain
1. Models (section 3.1) as `record` types; `CaptureSource` as an abstract record with three sealed
   subtypes.
2. Abstractions (interfaces) with XML doc comments on every member.
3. `RecordingSession`: state machine, countdown via `IClock`/`PeriodicTimer`, elapsed timer,
   events, `CancellationToken` on `StartAsync`.
4. `OutputPathResolver`: sanitises names, avoids collisions with ` (2)` suffix.
5. Tests: full transition table, countdown cancel, pause accounting, failure path, path collisions.

### Phase 2 — Infrastructure
1. `Win32CaptureSourceProvider`: displays via `EnumDisplayMonitors` + `GetMonitorInfo`
   (DPI-aware), top-level visible windows with titles via `EnumWindows` (skip tool windows,
   cloaked UWP windows via `DwmGetWindowAttribute(DWMWA_CLOAKED)`).
2. `ScreenRecorderLibEngine`: implements `IRecordingEngine`; maps options via
   `RecordingOptionsMapper`; subscribes to `OnRecordingComplete`, `OnRecordingFailed`,
   `OnStatusChanged`; guarantees `Dispose` stops the recorder.
3. `JsonSettingsStore`: atomic write (temp file + `File.Move` overwrite), defaults on missing or
   corrupt file.
4. `Win32HotkeyService`: `RegisterHotKey`/`UnregisterHotKey`, `WM_HOTKEY` via `HwndSource`,
   reports conflicts instead of throwing.
5. `WindowsShellService`: `explorer.exe /select,"file"`.
6. Tests: mapper (all option combinations), settings round-trip and corruption, path resolver.
7. Manual check: a 5-second full-screen recording produces a playable MP4.

### Phase 3 — Application shell and main flow
1. `App.xaml.cs`: build `IHost`, register services, single-instance guard (named `Mutex`),
   global exception handlers, Serilog file logging.
2. `MainViewModel`: exposes `Sources`, `SelectedSource`, audio/cursor toggles, `State`,
   `Elapsed`, `RecordCommand`, `PauseResumeCommand`, `StopCommand`, `OpenOutputFolderCommand`,
   `OpenSettingsCommand`. All `CanExecute` derive from `State`.
3. `MainWindow.xaml` per section 4 with styles in `Themes/`.
4. Hide window to tray on record start; restore on stop; tray menu mirrors commands.
5. `CountdownOverlay`: borderless, topmost, click-through, shows 3-2-1, closes before capture starts.
6. Tests: `MainViewModelTests` — commands enable/disable by state, settings applied to options,
   failure message shown.

### Phase 4 — Region and window capture
1. `RegionSelectorWindow`: full-virtual-screen transparent overlay, drag to draw a rectangle,
   shows size label, `Esc` cancels, `Enter`/mouse-up confirms; returns `ScreenRegion` in physical
   pixels (DPI-correct, multi-monitor).
2. Window picker: simple list dialog fed by `ICaptureSourceProvider`, refresh button.
3. Wire both into `SelectedSource`.

### Phase 5 — Settings, notifications, polish
1. `SettingsViewModel` + `SettingsView`; save on close; hotkey re-registration on change.
2. `WpfNotificationService`: small in-app banner after a recording with **Open file** /
   **Open folder**; auto-dismiss in 8 s.
3. Theme detection, high-DPI check (`PerMonitorV2` in `app.manifest`), app icon.
4. Settings migration guard (`SchemaVersion` field).

### Phase 6 — Quality gate
1. `dotnet format --verify-no-changes`, zero warnings (warnings are errors).
2. Manual test matrix (section 6.2) executed and recorded in `docs/TEST-REPORT.md`.
3. README: features, screenshots, build and run instructions, hotkeys, known limitations.

### Phase 7 — Packaging
1. `dotnet publish src/ScreenRecorder.App -c Release -r win-x64 --self-contained false
   -p:PublishSingleFile=true` (framework-dependent; smaller download; .NET 8 Desktop Runtime
   required). Document the alternative self-contained command.
2. Optional installer: Inno Setup script in `installer/` (start-menu shortcut, uninstaller).
3. Tag `v1.0.0`.

---

## 6. Testing strategy

### 6.1 Automated
- Unit tests for everything in `Core`, `RecordingOptionsMapper`, `JsonSettingsStore`,
  `OutputPathResolver`, and view models (with NSubstitute fakes).
- Target: ≥ 85 % line coverage on `Core`; view models fully covered for command enabling.
- Run on every build: `dotnet test --collect:"XPlat Code Coverage"`.

### 6.2 Manual matrix (run before release)
| # | Scenario | Expected |
|---|---|---|
| 1 | Full screen, system audio + mic, 30 fps, 30 s | MP4 plays in Windows Media Player and VLC, audio in sync |
| 2 | Secondary display | Correct display captured |
| 3 | Region on a 150 % DPI display | Exact region, no scaling offset |
| 4 | Window source, window moved mid-recording | Window content captured throughout |
| 5 | Pause 10 s, resume | Timer and file duration exclude the pause |
| 6 | Hotkeys with another app focused | Start, pause, stop all work |
| 7 | Close the main window while recording | Recording continues; tray shows state; stop from tray |
| 8 | Output folder on removed USB drive | Friendly error before countdown, no crash |
| 9 | Start a second instance | First instance is activated |
| 10 | Corrupt settings file | App starts with defaults, logs a warning |

---

## 7. Definition of done (v1.0)
- All phases 0–6 complete; `dotnet build` with zero warnings; all tests green.
- Manual matrix passes on Windows 10 22H2 and Windows 11.
- No class in `App` or `Infrastructure` exceeds ~250 lines; no `Core` type references Windows APIs.
- README and PLAN kept in sync with reality (update this file if a decision changes).

---

## 8. Execution notes for the implementing session
- Work phase by phase; do not start Phase N+1 until Phase N builds and its tests pass.
- Commit at the end of each phase with a message `feat(phase-N): …`.
- Prefer small files and explicit names over cleverness. No static service locators, no
  `async void` except event handlers, `ConfigureAwait` is unnecessary in WPF VMs but required in Core.
- If `ScreenRecorderLib` API differs from what the mapper expects, read the package's XML docs in
  `~/.nuget/packages/screenrecorderlib/<version>/lib/` first; do not guess member names.
- Keep the UI minimal. Every added control must justify itself against section 4.

---

## 9. Implementation record (v1.0.0)

Decisions that changed while implementing, and why.

| Plan said | Implemented | Why |
|---|---|---|
| `IClock` abstraction | `System.TimeProvider` (BCL) + `FakeTimeProvider` in tests | Built into .NET 8; no custom interface to maintain. |
| Session raises `ElapsedChanged` | Session exposes `Elapsed`; `MainViewModel` polls it every 250 ms with a `TimeProvider` timer | Keeps Core free of background ticking threads; UI owns refresh cadence. |
| `Failed ──Acknowledge──▶ Idle` | `Failed` is restartable directly (`CanStart()` is true for `Idle` and `Failed`) | One fewer transition; the UI never needed an explicit acknowledge step. |
| `ScreenRecorderLib` (version unspecified) | 7.0.1, audio via `LoopbackAudioSource` / `CaptureAudioSource` | 7.x fixes stalls on silent system audio; verified to load and record on .NET 8 despite targeting .NET Framework metadata. |
| Variable frame rate (library default) | `IsFixedFramerate = true` | Variable-rate MP4s confuse editors; verified 29–30 fps output. |
| `FluentAssertions` (latest) | 7.2.0 | 8.x requires a paid licence for commercial use. |
| Region selector covers the virtual screen | One overlay window per display | Correct on mixed-DPI setups; matches the engine's one-display-per-region model. |
| "Select region" button next to the source drop-down | "Window…" and "Region…" entries inside the drop-down | One control instead of two; a picked window/region stays in the list. |
| Settings as popover | Small modal dialog; global hotkeys suspended while it is open | Pressing the current shortcut while re-binding must not start a recording. |
| Notifications as toast | Inline banner in the main window (success auto-dismisses after 8 s, errors stay) | The window is restored after every recording, so the banner is always seen. |
| — | `IRecordingLauncher` (Core) | Builds options from settings + source; keeps `MainViewModel` thin and testable. |
| — | `RecordingUiCoordinator`, `HotkeyManager`, `TrayViewModel` (App) | Split window effects, hotkey sync and tray behaviour out of `MainViewModel` (SRP). |
| — | Countdown overlay uses `WDA_EXCLUDEFROMCAPTURE` | It can never appear in a recording, regardless of timing. |
| Single-file publish | Single-file exe **plus** `ScreenRecorderLib.dll` beside it | C++/CLI mixed-mode assemblies cannot load from a single-file bundle (verified: `BadImageFormatException`). |
| — | `SCREENRECORDER_THEME` environment override (`dark` or `light`) | Lets developers preview both themes without changing Windows settings. |

Repository pitfalls found:
- The stock Visual Studio `.gitignore` has a macOS `*.app` rule. Git on Windows is case-insensitive, so it silently ignored `src/ScreenRecorder.App/`. An explicit exception was added.
- The same file ignores `*.pubxml`; the release profile is whitelisted.
