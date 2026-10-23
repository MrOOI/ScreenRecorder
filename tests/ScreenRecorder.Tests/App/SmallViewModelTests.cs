using System.Windows;
using Microsoft.Extensions.Time.Testing;
using ScreenRecorder.App.Services;
using ScreenRecorder.App.ViewModels;
using ScreenRecorder.Core.Abstractions;
using ScreenRecorder.Core.Models;
using ScreenRecorder.Core.Services;
using ScreenRecorder.Tests.Fakes;

namespace ScreenRecorder.Tests.App;

public sealed class BannerViewModelTests
{
    private readonly IShellService _shell = Substitute.For<IShellService>();
    private readonly FakeTimeProvider _time = new();
    private readonly BannerViewModel _sut;

    public BannerViewModelTests()
    {
        _sut = new BannerViewModel(_shell, new ImmediateDispatcher(), _time);
    }

    [Fact]
    public void Success_auto_dismisses()
    {
        _sut.ShowSuccess("Saved", @"C:\a.mp4");
        _sut.IsVisible.Should().BeTrue();
        _sut.HasFile.Should().BeTrue();

        _time.Advance(BannerViewModel.SuccessDisplayTime);

        _sut.IsVisible.Should().BeFalse();
    }

    [Fact]
    public void Errors_stay_until_dismissed()
    {
        _sut.ShowError("Boom");
        _time.Advance(TimeSpan.FromMinutes(1));

        _sut.IsVisible.Should().BeTrue();
        _sut.IsError.Should().BeTrue();
        _sut.OpenFileCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void Old_timer_does_not_dismiss_a_newer_error()
    {
        _sut.ShowSuccess("Saved", @"C:\a.mp4");
        _sut.ShowError("Boom");

        _time.Advance(BannerViewModel.SuccessDisplayTime);

        _sut.IsVisible.Should().BeTrue();
    }

    [Fact]
    public void File_actions_use_shell_and_dismiss()
    {
        _sut.ShowSuccess("Saved", @"C:\a.mp4");
        _sut.OpenFileCommand.Execute(null);
        _shell.Received(1).OpenFile(@"C:\a.mp4");
        _sut.IsVisible.Should().BeFalse();

        _sut.ShowSuccess("Saved", @"C:\a.mp4");
        _sut.RevealFileCommand.Execute(null);
        _shell.Received(1).RevealFile(@"C:\a.mp4");
    }
}

public sealed class RegionSelectorViewModelTests
{
    [Fact]
    public void Drag_produces_physical_region_scaled_and_even()
    {
        var sut = new RegionSelectorViewModel { Scale = 1.5 };
        ScreenRegion? result = null;
        sut.Completed += (_, r) => result = r;

        sut.Begin(new Point(300, 200));
        sut.Update(new Point(100, 100));
        sut.SizeText.Should().Be("300 × 150");
        sut.End(new Point(100, 100));

        result.Should().Be(new ScreenRegion(150, 150, 300, 150));
    }

    [Fact]
    public void Tiny_drag_is_ignored_and_selection_can_restart()
    {
        var sut = new RegionSelectorViewModel();
        var completed = false;
        sut.Completed += (_, _) => completed = true;

        sut.Begin(new Point(10, 10));
        sut.End(new Point(14, 14));

        completed.Should().BeFalse();
        sut.ShowHint.Should().BeTrue();
    }

    [Fact]
    public void Cancel_completes_with_null()
    {
        var sut = new RegionSelectorViewModel();
        ScreenRegion? result = new ScreenRegion(1, 1, 1, 1);
        sut.Completed += (_, r) => result = r;

        sut.Cancel();

        result.Should().BeNull();
    }
}

public sealed class SettingsViewModelTests
{
    private readonly ISettingsStore _store = Substitute.For<ISettingsStore>();
    private readonly IFolderPicker _folderPicker = Substitute.For<IFolderPicker>();
    private readonly SettingsService _settings;

    public SettingsViewModelTests()
    {
        _store.Load().Returns(new AppSettings());
        _settings = new SettingsService(_store);
    }

    [Fact]
    public void Save_applies_all_edits_and_closes()
    {
        _folderPicker.PickFolder(Arg.Any<string>()).Returns(@"D:\Clips");
        var sut = new SettingsViewModel(_settings, _folderPicker);
        bool? closed = null;
        sut.CloseRequested += (_, saved) => closed = saved;

        sut.BrowseOutputFolderCommand.Execute(null);
        sut.FrameRate = 60;
        sut.Quality = VideoQuality.High;
        sut.CountdownEnabled = false;
        sut.SaveCommand.Execute(null);

        closed.Should().BeTrue();
        _settings.Current.Should().Match<AppSettings>(s =>
            s.OutputFolder == @"D:\Clips" && s.FrameRate == 60 && s.Quality == VideoQuality.High && !s.CountdownEnabled);
    }

    [Fact]
    public void Cancel_discards_edits()
    {
        var sut = new SettingsViewModel(_settings, _folderPicker);
        bool? closed = null;
        sut.CloseRequested += (_, saved) => closed = saved;

        sut.FrameRate = 60;
        sut.CancelCommand.Execute(null);

        closed.Should().BeFalse();
        _settings.Current.FrameRate.Should().Be(30);
    }

    [Fact]
    public void Identical_shortcuts_block_saving()
    {
        var sut = new SettingsViewModel(_settings, _folderPicker);

        sut.PauseResumeHotkey = sut.StartStopHotkey;
        sut.SaveCommand.Execute(null);

        sut.ErrorMessage.Should().NotBeNull();
        _store.DidNotReceive().Save(Arg.Any<AppSettings>());
    }
}

public sealed class TrayViewModelTests
{
    private readonly IRecordingSession _session = Substitute.For<IRecordingSession>();
    private readonly IAppLifetime _lifetime = Substitute.For<IAppLifetime>();
    private readonly IAppWindowService _window = Substitute.For<IAppWindowService>();

    [Fact]
    public void Exit_when_idle_shuts_down_immediately()
    {
        var sut = Create();

        sut.ExitCommand.Execute(null);

        _lifetime.Received(1).Shutdown();
    }

    [Fact]
    public void Exit_while_recording_stops_then_shuts_down_after_save()
    {
        _session.State.Returns(RecordingState.Recording);
        var sut = Create();

        sut.ExitCommand.Execute(null);
        _session.Received(1).Stop();
        _lifetime.DidNotReceive().Shutdown();

        _session.StateChanged += Raise.EventWith(_session,
            new RecordingStateChangedEventArgs(RecordingState.Finishing, RecordingState.Idle));

        _lifetime.Received(1).Shutdown();
    }

    [Fact]
    public void Show_window_restores_main_window()
    {
        Create().ShowWindowCommand.Execute(null);

        _window.Received(1).Restore();
    }

    private TrayViewModel Create()
    {
        var store = Substitute.For<ISettingsStore>();
        store.Load().Returns(new AppSettings());
        var dispatcher = new ImmediateDispatcher();
        var sources = Substitute.For<ICaptureSourceProvider>();
        sources.GetDisplays().Returns([TestData.PrimaryDisplay]);
        var selector = new SourceSelectorViewModel(sources, Substitute.For<IRegionPicker>(), Substitute.For<IDialogService>(),
            dispatcher, Microsoft.Extensions.Logging.Abstractions.NullLogger<SourceSelectorViewModel>.Instance);
        var main = new MainViewModel(_session, Substitute.For<IRecordingLauncher>(), new SettingsService(store),
            Substitute.For<INotificationService>(), Substitute.For<IShellService>(), Substitute.For<IDialogService>(),
            dispatcher, new FakeTimeProvider(), selector,
            new BannerViewModel(Substitute.For<IShellService>(), dispatcher, new FakeTimeProvider()),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<MainViewModel>.Instance);
        return new TrayViewModel(main, _window, _lifetime, _session, dispatcher);
    }
}
