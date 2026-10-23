using Microsoft.Extensions.Logging.Abstractions;
using ScreenRecorder.Core.Models;
using ScreenRecorder.Infrastructure.Recording;
using ScreenRecorder.Tests.Fakes;
using ScreenRecorderLib;

namespace ScreenRecorder.Tests.Infrastructure;

public sealed class RecordingOptionsMapperTests
{
    private readonly IAudioSourceFactory _audio = Substitute.For<IAudioSourceFactory>();
    private readonly RecordingOptionsMapper _sut;

    public RecordingOptionsMapperTests()
    {
        _sut = new RecordingOptionsMapper(_audio, NullLogger<RecordingOptionsMapper>.Instance);
    }

    [Fact]
    public void Display_source_maps_to_desktop_duplication_of_that_device()
    {
        var result = _sut.Map(TestData.Options(new DisplayCaptureSource(TestData.SecondaryDisplay)));

        var source = result.SourceOptions.RecordingSources.Should().ContainSingle()
            .Which.Should().BeOfType<DisplayRecordingSource>().Subject;
        source.DeviceName.Should().Be(TestData.SecondaryDisplay.DeviceName);
        source.RecorderApi.Should().Be(RecorderApi.DesktopDuplication);
        source.IsCursorCaptureEnabled.Should().BeTrue();
    }

    [Fact]
    public void Region_source_is_clipped_to_display_and_made_even()
    {
        var region = new RegionCaptureSource(TestData.PrimaryDisplay, new ScreenRegion(1800, 1000, 301, 201));

        var result = _sut.Map(TestData.Options(region));

        var rect = result.SourceOptions.RecordingSources.Single().SourceRect;
        (rect.Left, rect.Top, rect.Width, rect.Height).Should().Be((1800d, 1000d, 120d, 80d));
    }

    [Fact]
    public void Region_outside_display_is_rejected()
    {
        var region = new RegionCaptureSource(TestData.PrimaryDisplay, new ScreenRegion(5000, 5000, 100, 100));

        _sut.Invoking(s => s.Map(TestData.Options(region))).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Window_source_uses_graphics_capture_without_border()
    {
        var window = new WindowCaptureSource(new WindowInfo(1234, "Notepad", "notepad"));

        var result = _sut.Map(TestData.Options(window));

        var source = result.SourceOptions.RecordingSources.Single().Should().BeOfType<WindowRecordingSource>().Subject;
        source.Handle.Should().Be((nint)1234);
        source.RecorderApi.Should().Be(RecorderApi.WindowsGraphicsCapture);
        source.IsBorderRequired.Should().BeFalse();
    }

    [Theory]
    [InlineData(false, false, 0)]
    [InlineData(true, false, 1)]
    [InlineData(false, true, 1)]
    [InlineData(true, true, 2)]
    public void Audio_sources_follow_toggles(bool system, bool microphone, int expectedSources)
    {
        _audio.CreateSystemAudioSource().Returns(_ => new LoopbackAudioSource("loopback-id"));
        _audio.CreateMicrophoneSource().Returns(_ => new CaptureAudioSource("mic-id"));
        var options = TestData.Options() with { CaptureSystemAudio = system, CaptureMicrophone = microphone };

        var result = _sut.Map(options);

        result.AudioOptions.AudioSources.Should().HaveCount(expectedSources);
        result.AudioOptions.IsAudioEnabled.Should().Be(expectedSources > 0);
    }

    [Fact]
    public void Missing_audio_device_records_without_it()
    {
        _audio.CreateSystemAudioSource().Returns((AudioSourceBase?)null);

        var result = _sut.Map(TestData.Options() with { CaptureSystemAudio = true, CaptureMicrophone = false });

        result.AudioOptions.AudioSources.Should().BeEmpty();
        result.AudioOptions.IsAudioEnabled.Should().BeFalse();
    }

    [Theory]
    [InlineData(VideoQuality.Low, 50)]
    [InlineData(VideoQuality.Medium, 70)]
    [InlineData(VideoQuality.High, 90)]
    public void Quality_and_frame_rate_map_to_encoder(VideoQuality quality, int expectedPercent)
    {
        var result = _sut.Map(TestData.Options() with { Quality = quality, FrameRate = 60 });

        result.VideoEncoderOptions.Quality.Should().Be(expectedPercent);
        result.VideoEncoderOptions.Framerate.Should().Be(60);
        result.VideoEncoderOptions.IsFixedFramerate.Should().BeTrue();
        result.VideoEncoderOptions.Encoder.Should().BeOfType<H264VideoEncoder>();
    }

    [Fact]
    public void Cursor_toggle_controls_mouse_pointer()
    {
        var result = _sut.Map(TestData.Options() with { ShowCursor = false });

        result.MouseOptions.IsMousePointerEnabled.Should().BeFalse();
        ((DisplayRecordingSource)result.SourceOptions.RecordingSources.Single()).IsCursorCaptureEnabled.Should().BeFalse();
    }
}
