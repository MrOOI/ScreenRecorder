using ScreenRecorder.Core.Models;
using ScreenRecorder.Infrastructure.Recording;

namespace ScreenRecorder.Tests.Infrastructure;

public sealed class VideoBitrateTests
{
    [Theory]
    [InlineData(VideoQuality.Low, 3_732_480)]
    [InlineData(VideoQuality.Medium, 7_464_960)]
    [InlineData(VideoQuality.High, 12_441_600)]
    public void Full_hd_at_30_fps_gets_a_text_friendly_budget(VideoQuality quality, int expected) =>
        VideoBitrate.For(1920, 1080, 30, quality).Should().Be(expected);

    [Fact]
    public void Higher_presets_never_get_fewer_bits() =>
        new[] { VideoQuality.Low, VideoQuality.Medium, VideoQuality.High }
            .Select(q => VideoBitrate.For(1920, 1080, 30, q))
            .Should().BeInAscendingOrder();

    [Fact]
    public void Tiny_regions_get_the_minimum() =>
        VideoBitrate.For(64, 64, 30, VideoQuality.Low).Should().Be(VideoBitrate.MinBitsPerSecond);

    [Fact]
    public void Huge_captures_are_capped() =>
        VideoBitrate.For(7680, 4320, 60, VideoQuality.High).Should().Be(VideoBitrate.MaxBitsPerSecond);

    [Fact]
    public void Empty_size_is_rejected() =>
        FluentActions.Invoking(() => VideoBitrate.For(0, 1080, 30, VideoQuality.Medium))
            .Should().Throw<ArgumentOutOfRangeException>();
}
