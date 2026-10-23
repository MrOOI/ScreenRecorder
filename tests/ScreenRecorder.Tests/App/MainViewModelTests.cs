using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using ScreenRecorder.App.Services;
using ScreenRecorder.App.ViewModels;
using ScreenRecorder.Core.Abstractions;
using ScreenRecorder.Core.Models;
using ScreenRecorder.Core.Services;
using ScreenRecorder.Tests.Fakes;

namespace ScreenRecorder.Tests.App;

public sealed class MainViewModelTests : IDisposable
{
    private readonly IRecordingSession _session = Substitute.For<IRecordingSession>();
    private readonly IRecordingLauncher _launcher = Substitute.For<IRecordingLauncher>();
    private readonly INotificationService _notifications = Substitute.For<INotificationService>();
    private readonly IShellService _shell = Substitute.For<IShellService>();
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly IRegionPicker _regionPicker = Substitute.For<IRegionPicker>();
    private readonly ISettingsStore _store = Substitute.For<ISettingsStore>();
    private readonly FakeTimeProvider _time = new();
    private readonly SettingsService _settings;
    private readonly MainViewModel _sut;

    public MainViewModelTests()
    {
        _store.Load().Returns(new AppSettings { OutputFolder = @"C:\Users\me\Videos\ScreenRecorder" });
        _settings = new SettingsService(_store);

        var sources = Substitute.For<ICaptureSourceProvider>();
        sources.GetDisplays().Returns([TestData.PrimaryDisplay]);
        var dispatcher = new ImmediateDispatcher();
        var selector = new SourceSelectorViewModel(sources, _regionPicker, _dialogs, dispatcher,
            NullLogger<SourceSelectorViewModel>.Instance);
        var banner = new BannerViewModel(_shell, dispatcher, _time);

        _sut = new MainViewModel(_session, _launcher, _settings, _notifications, _shell, _dialogs, dispatcher, _time,
            selector, banner, NullLogger<MainViewModel>.Instance);
    }

    public void Dispose() => _sut.Dispose();

    [Fact]
    public void Starts_ready_with_record_enabled()
    {
        _sut.State.Should().Be(RecordingState.Idle);
        _sut.PrimaryActionText.Should().Be("Record");
        _sut.StatusText.Should().Be("Ready");
        _sut.ElapsedText.Should().Be("00:00:00");
        _sut.IsPauseVisible.Should().BeFalse();
        _sut.ToggleRecordingCommand.CanExecute(null).Should().BeTrue();
        _sut.PauseResumeCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public async Task Record_starts_selected_source()
    {
        await _sut.ToggleRecordingCommand.ExecuteAsync(null);

        await _launcher.Received(1).StartAsync(new DisplayCaptureSource(TestData.PrimaryDisplay), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Unusable_output_folder_is_reported()
    {
        _launcher.StartAsync(Arg.Any<CaptureSource>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new OutputFolderUnavailableException("Can't save there.")));

        await _sut.ToggleRecordingCommand.ExecuteAsync(null);

        _notifications.Received(1).ShowError("Can't save there.");
    }

    [Fact]
    public void Record_is_disabled_while_a_picker_entry_is_selected()
    {
        // The overlay is still open: the pick never completes during this test.
        _regionPicker.PickRegionAsync().Returns(new TaskCompletionSource<RegionCaptureSource?>().Task);

        _sut.Sources.Selected = SourceOption.PickRegion;

        _sut.ToggleRecordingCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void Recording_state_switches_to_stop_and_pause()
    {
        MoveTo(RecordingState.Recording);

        _sut.PrimaryActionText.Should().Be("Stop");
        _sut.IsActive.Should().BeTrue();
        _sut.CanChangeOptions.Should().BeFalse();
        _sut.IsPauseVisible.Should().BeTrue();
        _sut.PauseResumeText.Should().Be("Pause");
        _sut.PauseResumeCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public async Task Stop_while_recording_stops_session()
    {
        MoveTo(RecordingState.Recording);

        await _sut.ToggleRecordingCommand.ExecuteAsync(null);

        _session.Received(1).Stop();
        await _launcher.DidNotReceiveWithAnyArgs().StartAsync(default!, default);
    }

    [Fact]
    public void Pause_and_resume_follow_state()
    {
        MoveTo(RecordingState.Recording);
        _sut.PauseResumeCommand.Execute(null);
        _session.Received(1).Pause();

        MoveTo(RecordingState.Paused);
        _sut.PauseResumeText.Should().Be("Resume");
        _sut.PauseResumeCommand.Execute(null);
        _session.Received(1).Resume();
    }

    [Fact]
    public void Finishing_disables_all_recording_commands()
    {
        MoveTo(RecordingState.Finishing);

        _sut.ToggleRecordingCommand.CanExecute(null).Should().BeFalse();
        _sut.PauseResumeCommand.CanExecute(null).Should().BeFalse();
        _sut.StatusText.Should().Be("Saving…");
    }

    [Fact]
    public void Countdown_status_shows_seconds_remaining()
    {
        MoveTo(RecordingState.Countdown);
        _session.CountdownTick += Raise.EventWith(_session, new CountdownTickEventArgs(2));

        _sut.StatusText.Should().Be("Starting in 2…");
        _sut.ToggleRecordingCommand.CanExecute(null).Should().BeTrue("Stop must be able to cancel the countdown");
    }

    [Fact]
    public void Elapsed_timer_ticks_while_recording()
    {
        _session.Elapsed.Returns(TimeSpan.Zero);
        MoveTo(RecordingState.Recording);

        _session.Elapsed.Returns(TimeSpan.FromSeconds(75));
        _time.Advance(MainViewModel.TimerInterval);

        _sut.ElapsedText.Should().Be("00:01:15");
    }

    [Fact]
    public void Toggles_write_through_to_settings()
    {
        _sut.CaptureMicrophone = true;
        _sut.ShowCursor = false;

        _settings.Current.CaptureMicrophone.Should().BeTrue();
        _settings.Current.ShowCursor.Should().BeFalse();
    }

    [Fact]
    public void Output_folder_display_shows_last_two_segments()
    {
        _sut.OutputFolderDisplay.Should().Be(Path.Combine("Videos", "ScreenRecorder"));
    }

    [Fact]
    public void Commands_delegate_to_shell_and_dialogs()
    {
        _sut.OpenOutputFolderCommand.Execute(null);
        _sut.OpenSettingsCommand.Execute(null);

        _shell.Received(1).OpenFolder(@"C:\Users\me\Videos\ScreenRecorder");
        _dialogs.Received(1).ShowSettings();
    }

    [Fact]
    public void Hotkeys_route_to_commands()
    {
        MoveTo(RecordingState.Recording);

        _sut.HandleHotkey(HotkeyAction.PauseResume);
        _sut.HandleHotkey(HotkeyAction.StartStop);

        _session.Received(1).Pause();
        _session.Received(1).Stop();
    }

    private void MoveTo(RecordingState state)
    {
        var old = _session.State;
        _session.State.Returns(state);
        _session.StateChanged += Raise.EventWith(_session, new RecordingStateChangedEventArgs(old, state));
    }
}
