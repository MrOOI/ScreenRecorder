using ScreenRecorder.Core.Models;

namespace ScreenRecorder.Infrastructure.Recording;

/// <summary>
/// Target H.264 bitrate for screen content. Screen video is mostly static with sharp, high-contrast
/// edges (text), so the budget scales with pixels per second rather than being left to the encoder:
/// quality-based rate control on hardware encoders starves static frames and smears text.
/// </summary>
internal static class VideoBitrate
{
    public const int MinBitsPerSecond = 1_000_000;
    public const int MaxBitsPerSecond = 80_000_000;

    /// <summary>Bits spent per pixel per frame. Medium gives ~7.5 Mbps at 1080p30.</summary>
    internal static double BitsPerPixel(VideoQuality quality) => quality switch
    {
        VideoQuality.Low => 0.06,
        VideoQuality.Medium => 0.12,
        VideoQuality.High => 0.20,
        _ => throw new ArgumentOutOfRangeException(nameof(quality), quality, null),
    };

    public static int For(int width, int height, int frameRate, VideoQuality quality)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(frameRate);

        var bits = (double)width * height * frameRate * BitsPerPixel(quality);
        return (int)Math.Round(Math.Clamp(bits, MinBitsPerSecond, MaxBitsPerSecond));
    }
}
