using ScreenRecorder.Core.Abstractions;
using ScreenRecorder.Core.Models;
using ScreenRecorder.Core.Services;
using ScreenRecorder.Tests.Fakes;

namespace ScreenRecorder.Tests.Core;

public sealed class RecordingLauncherTests
{
    private readonly IRecordingSession _session = Substitute.For<IRecordingSession>();
    private readonly ISettingsService _settings = Substitute.For<ISettingsService>();
    private readonly IOutputPathResolver _paths = Substitute.For<IOutputPathResolver>();
    private readonly RecordingLauncher _sut;

    public RecordingLauncherTests()
    {
        _sut = new RecordingLauncher(_session, _settings, _paths);
    }

    [Fact]
    public async Task Builds_options_from_settings_and_starts_session()
    {
        _settings.Current.Returns(new AppSettings
        {
            OutputFolder = @"D:\Out",
            CaptureSystemAudio = false,
            CaptureMicrophone = true,
            ShowCursor = false,
            FrameRate = 60,
            Quality = VideoQuality.High,
            CountdownEnabled = true,
            CountdownSeconds = 5,
        });
        _paths.ResolveNewFilePath(@"D:\Out").Returns(@"D:\Out\Recording.mp4");
        var source = new DisplayCaptureSource(TestData.PrimaryDisplay);

        await _sut.StartAsync(source);

        await _session.Received(1).StartAsync(
            new RecordingOptions(source, @"D:\Out\Recording.mp4", false, true, false, 60, VideoQuality.High),
            TimeSpan.FromSeconds(5),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Unusable_folder_fails_before_session_starts()
    {
        _settings.Current.Returns(new AppSettings());
        _paths.ResolveNewFilePath(Arg.Any<string>()).Returns(_ => throw new OutputFolderUnavailableException("nope"));

        await _sut.Invoking(s => s.StartAsync(new DisplayCaptureSource(TestData.PrimaryDisplay)))
            .Should().ThrowAsync<OutputFolderUnavailableException>();

        await _session.DidNotReceiveWithAnyArgs().StartAsync(default!, default, default);
    }
}
