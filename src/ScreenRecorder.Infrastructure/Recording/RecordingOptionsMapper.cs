using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using ScreenRecorder.Core.Models;
using ScreenRecorder.Infrastructure.Interop;
using ScreenRecorderLib;

namespace ScreenRecorder.Infrastructure.Recording;

/// <summary>
/// The only place that knows how application options translate into ScreenRecorderLib options.
/// </summary>
internal sealed class RecordingOptionsMapper(IAudioSourceFactory audioSources, ILogger<RecordingOptionsMapper> logger)
{
    public RecorderOptions Map(RecordingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new RecorderOptions
        {
            SourceOptions = new SourceOptions { RecordingSources = { CreateSource(options.Source, options.ShowCursor) } },
            AudioOptions = CreateAudio(options),
            MouseOptions = new MouseOptions { IsMousePointerEnabled = options.ShowCursor },
            OutputOptions = new OutputOptions { RecorderMode = RecorderMode.Video },
            VideoEncoderOptions = new VideoEncoderOptions
            {
                Encoder = new H264VideoEncoder
                {
                    // An explicit bitrate target, not quality-based VBR: hardware encoders in quality
                    // mode drop static screen content to a few hundred kbps and text becomes unreadable.
                    BitrateMode = H264BitrateControlMode.UnconstrainedVBR,
                    EncoderProfile = H264Profile.High,
                },
                Bitrate = TargetBitrate(options),
                Framerate = options.FrameRate,
                // Constant frame rate: variable-rate files confuse many editors and some players.
                IsFixedFramerate = true,
                IsHardwareEncodingEnabled = true,
                IsMp4FastStartEnabled = true,
            },
        };
    }

    internal static int TargetBitrate(RecordingOptions options)
    {
        var (width, height) = FrameSize(options.Source);
        return VideoBitrate.For(width, height, options.FrameRate, options.Quality);
    }

    private static (int Width, int Height) FrameSize(CaptureSource source) => source switch
    {
        DisplayCaptureSource display => SizeOf(display.Display.Bounds),
        RegionCaptureSource region => SizeOf(ClipToDisplay(region)),
        WindowCaptureSource window => WindowSize(window.Window.Handle),
        _ => throw new NotSupportedException($"Unsupported capture source {source.GetType().Name}."),
    };

    private static (int Width, int Height) SizeOf(ScreenRegion region) => (region.Width, region.Height);

    /// <summary>The window's visible bounds; a 1080p budget if the window cannot be measured.</summary>
    private static (int Width, int Height) WindowSize(nint handle)
    {
        var hr = NativeMethods.DwmGetWindowRectAttribute(handle, NativeMethods.DwmwaExtendedFrameBounds,
            out var rect, Marshal.SizeOf<NativeMethods.Rect>());
        var (width, height) = (rect.Right - rect.Left, rect.Bottom - rect.Top);
        return hr == 0 && width > 0 && height > 0 ? (width, height) : (1920, 1080);
    }

    private static RecordingSourceBase CreateSource(CaptureSource source, bool showCursor) => source switch
    {
        DisplayCaptureSource display => new DisplayRecordingSource(display.Display.DeviceName)
        {
            RecorderApi = RecorderApi.DesktopDuplication,
            IsCursorCaptureEnabled = showCursor,
        },
        RegionCaptureSource region => new DisplayRecordingSource(region.Display.DeviceName)
        {
            RecorderApi = RecorderApi.DesktopDuplication,
            IsCursorCaptureEnabled = showCursor,
            SourceRect = ToScreenRect(region),
        },
        // Window sources always use Graphics Capture, which records the window even when it is covered.
        WindowCaptureSource window => new WindowRecordingSource(window.Window.Handle)
        {
            IsCursorCaptureEnabled = showCursor,
            IsBorderRequired = false,
        },
        _ => throw new NotSupportedException($"Unsupported capture source {source.GetType().Name}."),
    };

    private static ScreenRect ToScreenRect(RegionCaptureSource region)
    {
        var clipped = ClipToDisplay(region);
        return new ScreenRect(clipped.X, clipped.Y, clipped.Width, clipped.Height);
    }

    private static ScreenRegion ClipToDisplay(RegionCaptureSource region)
    {
        var displaySize = new ScreenRegion(0, 0, region.Display.Bounds.Width, region.Display.Bounds.Height);
        var clipped = region.Region.Intersect(displaySize).WithEvenSize();
        if (clipped.IsEmpty)
        {
            throw new ArgumentException("The selected region is outside the display.", nameof(region));
        }

        return clipped;
    }

    private AudioOptions CreateAudio(RecordingOptions options)
    {
        var audio = new AudioOptions
        {
            Bitrate = AudioBitrate.bitrate_192kbps,
            Channels = AudioChannels.Stereo,
        };

        if (options.CaptureSystemAudio)
        {
            AddOrWarn(audio, audioSources.CreateSystemAudioSource(), "system audio");
        }

        if (options.CaptureMicrophone)
        {
            AddOrWarn(audio, audioSources.CreateMicrophoneSource(), "microphone");
        }

        audio.IsAudioEnabled = audio.AudioSources.Count > 0;
        return audio;
    }

    private void AddOrWarn(AudioOptions audio, AudioSourceBase? source, string kind)
    {
        if (source is null)
        {
            logger.LogWarning("No default {Kind} device found; recording without it", kind);
            return;
        }

        audio.AudioSources.Add(source);
    }
}
