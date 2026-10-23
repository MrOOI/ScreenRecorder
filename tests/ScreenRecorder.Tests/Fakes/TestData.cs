using Microsoft.Extensions.Time.Testing;
using ScreenRecorder.Core.Models;

namespace ScreenRecorder.Tests.Fakes;

internal static class TestData
{
    public static readonly DisplayInfo PrimaryDisplay =
        new(@"\\.\DISPLAY1", 1, new ScreenRegion(0, 0, 1920, 1080), IsPrimary: true);

    public static readonly DisplayInfo SecondaryDisplay =
        new(@"\\.\DISPLAY2", 2, new ScreenRegion(1920, 0, 2560, 1440), IsPrimary: false);

    public static RecordingOptions Options(CaptureSource? source = null) => new(
        source ?? new DisplayCaptureSource(PrimaryDisplay),
        @"C:\Videos\test.mp4",
        CaptureSystemAudio: true,
        CaptureMicrophone: false,
        ShowCursor: true,
        FrameRate: 30,
        Quality: VideoQuality.Medium);

    /// <summary>
    /// Advances fake time in small steps until <paramref name="condition"/> holds.
    /// Steps are interleaved with real yields so async continuations can register their next timer.
    /// </summary>
    public static async Task AdvanceUntilAsync(this FakeTimeProvider time, Func<bool> condition, int maxSteps = 500)
    {
        for (var i = 0; i < maxSteps && !condition(); i++)
        {
            time.Advance(TimeSpan.FromMilliseconds(100));
            await Task.Delay(1);
        }

        condition().Should().BeTrue("the condition should be reached within {0} fake-time steps", maxSteps);
    }
}
