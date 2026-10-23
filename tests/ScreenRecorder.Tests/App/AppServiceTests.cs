using Microsoft.Extensions.Logging.Abstractions;
using ScreenRecorder.App.Services;
using ScreenRecorder.App.ViewModels;
using ScreenRecorder.Core.Abstractions;
using ScreenRecorder.Core.Models;
using ScreenRecorder.Tests.Fakes;

namespace ScreenRecorder.Tests.App;

public sealed class RecordingUiCoordinatorTests
{
    private readonly IRecordingSession _session = Substitute.For<IRecordingSession>();
    private readonly IAppWindowService _window = Substitute.For<IAppWindowService>();
    private readonly ICountdownPresenter _countdown = Substitute.For<ICountdownPresenter>();
    private readonly INotificationService _notifications = Substitute.For<INotificationService>();

    public RecordingUiCoordinatorTests()
    {
        new RecordingUiCoordinator(_session, new ImmediateDispatcher(), _window, _countdown, _notifications).Start();
    }

    [Fact]
    public void Countdown_hides_window_and_shows_ticks()
    {
        Change(RecordingState.Idle, RecordingState.Countdown);
        Tick(3);
        Tick(0);

        Received.InOrder(() =>
        {
            _window.Hide();
            _countdown.Show(3);
            _countdown.Hide();
        });
    }

    [Fact]
    public void End_of_recording_restores_window()
    {
        Change(RecordingState.Finishing, RecordingState.Idle);

        _window.Received(1).Restore();
        _countdown.Received(1).Hide();
    }

    [Fact]
    public void Failure_restores_window_too()
    {
        Change(RecordingState.Recording, RecordingState.Failed);

        _window.Received(1).Restore();
    }

    [Fact]
    public void Finished_result_is_reported()
    {
        var result = RecordingResult.Success(@"C:\a.mp4", TimeSpan.FromSeconds(3));

        _session.Finished += Raise.EventWith(_session, new RecordingFinishedEventArgs(result));

        _notifications.Received(1).ShowRecordingResult(result);
    }

    private void Change(RecordingState from, RecordingState to) =>
        _session.StateChanged += Raise.EventWith(_session, new RecordingStateChangedEventArgs(from, to));

    private void Tick(int seconds) =>
        _session.CountdownTick += Raise.EventWith(_session, new CountdownTickEventArgs(seconds));
}

public sealed class HotkeyManagerTests : IDisposable
{
    private readonly IHotkeyService _hotkeys = Substitute.For<IHotkeyService>();
    private readonly ISettingsService _settings = Substitute.For<ISettingsService>();
    private readonly INotificationService _notifications = Substitute.For<INotificationService>();
    private readonly HotkeyManager _sut;
    private readonly AppSettings _defaults = new();

    public HotkeyManagerTests()
    {
        _settings.Current.Returns(_defaults);
        _hotkeys.TryRegister(Arg.Any<HotkeyAction>(), Arg.Any<Hotkey>()).Returns(true);
        _sut = new HotkeyManager(_hotkeys, _settings, _notifications, NullLogger<HotkeyManager>.Instance);
    }

    public void Dispose() => _sut.Dispose();

    [Fact]
    public void Start_registers_both_hotkeys()
    {
        _sut.Start();

        _hotkeys.Received(1).TryRegister(HotkeyAction.StartStop, _defaults.StartStopHotkey);
        _hotkeys.Received(1).TryRegister(HotkeyAction.PauseResume, _defaults.PauseResumeHotkey);
        _notifications.DidNotReceive().ShowError(Arg.Any<string>());
    }

    [Fact]
    public void Conflicts_are_reported_to_the_user()
    {
        _hotkeys.TryRegister(HotkeyAction.StartStop, Arg.Any<Hotkey>()).Returns(false);

        _sut.Start();

        _notifications.Received(1).ShowError(Arg.Is<string>(m => m.StartsWith("Ctrl+Shift+R is already used")));
    }

    [Fact]
    public void Suspend_releases_and_restores_registrations()
    {
        _sut.Start();
        _hotkeys.ClearReceivedCalls();

        using (_sut.Suspend())
        {
            _hotkeys.Received(1).UnregisterAll();
            _hotkeys.DidNotReceiveWithAnyArgs().TryRegister(default, default!);
        }

        _hotkeys.Received(2).TryRegister(Arg.Any<HotkeyAction>(), Arg.Any<Hotkey>());
    }

    [Fact]
    public void Changed_shortcuts_are_re_registered()
    {
        _sut.Start();
        _hotkeys.ClearReceivedCalls();
        var updated = _defaults with { StartStopHotkey = new Hotkey(HotkeyModifiers.Alt, 'R') };
        _settings.Current.Returns(updated);

        _settings.Changed += Raise.EventWith(_settings, new SettingsChangedEventArgs(_defaults, updated));

        _hotkeys.Received(1).TryRegister(HotkeyAction.StartStop, updated.StartStopHotkey);
    }

    [Fact]
    public void Presses_are_forwarded()
    {
        _sut.Start();
        HotkeyAction? triggered = null;
        _sut.Triggered += (_, e) => triggered = e.Action;

        _hotkeys.Pressed += Raise.EventWith(_hotkeys, new HotkeyPressedEventArgs(HotkeyAction.PauseResume));

        triggered.Should().Be(HotkeyAction.PauseResume);
    }
}

public sealed class BannerNotificationServiceTests
{
    [Fact]
    public void Success_shows_duration_and_file_name()
    {
        var banner = new BannerViewModel(Substitute.For<IShellService>(), new ImmediateDispatcher(), TimeProvider.System);
        var sut = new BannerNotificationService(banner, new ImmediateDispatcher());

        sut.ShowRecordingResult(RecordingResult.Success(@"C:\Videos\Recording 1.mp4", TimeSpan.FromSeconds(83)));

        banner.Message.Should().Be("Saved 1:23 · Recording 1.mp4");
        banner.FilePath.Should().Be(@"C:\Videos\Recording 1.mp4");
        banner.Dismiss();
    }

    [Fact]
    public void Failure_shows_error()
    {
        var banner = new BannerViewModel(Substitute.For<IShellService>(), new ImmediateDispatcher(), TimeProvider.System);
        var sut = new BannerNotificationService(banner, new ImmediateDispatcher());

        sut.ShowRecordingResult(RecordingResult.Failure("Display lost.", TimeSpan.Zero));

        banner.IsError.Should().BeTrue();
        banner.Message.Should().Be("Display lost.");
    }
}
