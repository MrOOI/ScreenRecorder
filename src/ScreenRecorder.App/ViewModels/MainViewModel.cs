using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using ScreenRecorder.App.Services;
using ScreenRecorder.Core.Abstractions;
using ScreenRecorder.Core.Models;

namespace ScreenRecorder.App.ViewModels;

/// <summary>
/// The main window: source, capture toggles, the record/stop button and the timer.
/// </summary>
public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    public static readonly TimeSpan TimerInterval = TimeSpan.FromMilliseconds(250);

    private readonly IRecordingSession _session;
    private readonly IRecordingLauncher _launcher;
    private readonly ISettingsService _settings;
    private readonly INotificationService _notifications;
    private readonly IShellService _shell;
    private readonly IDialogService _dialogs;
    private readonly IUiDispatcher _dispatcher;
    private readonly TimeProvider _time;
    private readonly ILogger<MainViewModel> _logger;
    private ITimer? _elapsedTimer;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsActive), nameof(CanChangeOptions), nameof(PrimaryActionText),
        nameof(IsPauseVisible), nameof(PauseResumeText), nameof(StatusText))]
    [NotifyCanExecuteChangedFor(nameof(ToggleRecordingCommand), nameof(PauseResumeCommand))]
    private RecordingState _state;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private int _countdownRemaining;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ElapsedText))]
    private TimeSpan _elapsed;

    public MainViewModel(
        IRecordingSession session,
        IRecordingLauncher launcher,
        ISettingsService settings,
        INotificationService notifications,
        IShellService shell,
        IDialogService dialogs,
        IUiDispatcher dispatcher,
        TimeProvider time,
        SourceSelectorViewModel sources,
        BannerViewModel banner,
        ILogger<MainViewModel> logger)
    {
        _session = session;
        _launcher = launcher;
        _settings = settings;
        _notifications = notifications;
        _shell = shell;
        _dialogs = dialogs;
        _dispatcher = dispatcher;
        _time = time;
        _logger = logger;
        Sources = sources;
        Banner = banner;
        _state = session.State;

        Sources.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SourceSelectorViewModel.SelectedSource))
            {
                ToggleRecordingCommand.NotifyCanExecuteChanged();
            }
        };
        _session.StateChanged += OnSessionStateChanged;
        _session.CountdownTick += OnSessionCountdownTick;
        _settings.Changed += OnSettingsChanged;
    }

    public SourceSelectorViewModel Sources { get; }

    public BannerViewModel Banner { get; }

    public bool IsActive => State.IsActive();

    public bool CanChangeOptions => State.CanStart();

    public bool IsPauseVisible => State is RecordingState.Recording or RecordingState.Paused;

    public string ElapsedText => Elapsed.ToString(@"hh\:mm\:ss");

    public string PrimaryActionText => IsActive ? "Stop" : "Record";

    public string PauseResumeText => State == RecordingState.Paused ? "Resume" : "Pause";

    public string StatusText => DisplayText.ForStatus(State, CountdownRemaining);

    public string OutputFolder => _settings.Current.OutputFolder;

    public string OutputFolderDisplay => DisplayText.ForFolder(OutputFolder);

    public bool CaptureSystemAudio
    {
        get => _settings.Current.CaptureSystemAudio;
        set => _settings.Update(s => s with { CaptureSystemAudio = value });
    }

    public bool CaptureMicrophone
    {
        get => _settings.Current.CaptureMicrophone;
        set => _settings.Update(s => s with { CaptureMicrophone = value });
    }

    public bool ShowCursor
    {
        get => _settings.Current.ShowCursor;
        set => _settings.Update(s => s with { ShowCursor = value });
    }

    /// <summary>Routes a global hotkey to the matching command.</summary>
    public void HandleHotkey(HotkeyAction action)
    {
        var command = action switch
        {
            HotkeyAction.StartStop => (IRelayCommand)ToggleRecordingCommand,
            HotkeyAction.PauseResume => PauseResumeCommand,
            _ => null,
        };

        if (command?.CanExecute(null) == true)
        {
            command.Execute(null);
        }
    }

    public void Dispose()
    {
        StopElapsedTimer();
        _session.StateChanged -= OnSessionStateChanged;
        _session.CountdownTick -= OnSessionCountdownTick;
        _settings.Changed -= OnSettingsChanged;
    }

    private bool CanToggleRecording() => State switch
    {
        RecordingState.Finishing => false,
        _ when State.CanStart() => Sources.SelectedSource is not null,
        _ => true,
    };

    // Concurrent executions are allowed so Stop can cancel a countdown that Record is still awaiting.
    [RelayCommand(CanExecute = nameof(CanToggleRecording), AllowConcurrentExecutions = true)]
    private async Task ToggleRecordingAsync()
    {
        if (!State.CanStart())
        {
            _session.Stop();
            return;
        }

        if (Sources.SelectedSource is not { } source)
        {
            return;
        }

        try
        {
            Banner.Dismiss();
            await _launcher.StartAsync(source);
        }
        catch (OutputFolderUnavailableException ex)
        {
            _notifications.ShowError(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            // A hotkey and a click raced; the session already reflects the winner.
            _logger.LogDebug(ex, "Start ignored");
        }
    }

    private bool CanPauseResume() => IsPauseVisible;

    [RelayCommand(CanExecute = nameof(CanPauseResume))]
    private void PauseResume()
    {
        try
        {
            if (State == RecordingState.Paused)
            {
                _session.Resume();
            }
            else
            {
                _session.Pause();
            }
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogDebug(ex, "Pause/resume ignored");
        }
    }

    [RelayCommand]
    private void OpenOutputFolder() => _shell.OpenFolder(OutputFolder);

    [RelayCommand]
    private void OpenSettings() => _dialogs.ShowSettings();

    private void OnSessionStateChanged(object? sender, RecordingStateChangedEventArgs e) =>
        _dispatcher.Post(() => ApplyState(e.NewState));

    private void OnSessionCountdownTick(object? sender, CountdownTickEventArgs e) =>
        _dispatcher.Post(() => CountdownRemaining = e.SecondsRemaining);

    private void OnSettingsChanged(object? sender, SettingsChangedEventArgs e) => _dispatcher.Post(() =>
    {
        OnPropertyChanged(nameof(CaptureSystemAudio));
        OnPropertyChanged(nameof(CaptureMicrophone));
        OnPropertyChanged(nameof(ShowCursor));
        OnPropertyChanged(nameof(OutputFolder));
        OnPropertyChanged(nameof(OutputFolderDisplay));
    });

    private void ApplyState(RecordingState state)
    {
        State = state;
        Elapsed = _session.Elapsed;

        if (state is RecordingState.Recording or RecordingState.Paused)
        {
            _elapsedTimer ??= _time.CreateTimer(
                _ => _dispatcher.Post(() => Elapsed = _session.Elapsed), null, TimerInterval, TimerInterval);
        }
        else
        {
            StopElapsedTimer();
        }

        if (state.CanStart())
        {
            // Pick up displays connected or removed while recording.
            Sources.Refresh();
        }
    }

    private void StopElapsedTimer()
    {
        _elapsedTimer?.Dispose();
        _elapsedTimer = null;
    }
}
