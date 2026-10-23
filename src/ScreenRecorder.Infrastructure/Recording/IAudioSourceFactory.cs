using ScreenRecorderLib;

namespace ScreenRecorder.Infrastructure.Recording;

/// <summary>
/// Resolves the system's default audio devices. Separated so mapping stays testable without hardware.
/// </summary>
internal interface IAudioSourceFactory
{
    /// <summary>The default playback device in loopback mode, or null when there is none.</summary>
    AudioSourceBase? CreateSystemAudioSource();

    /// <summary>The default capture device, or null when there is none.</summary>
    AudioSourceBase? CreateMicrophoneSource();
}

internal sealed class DefaultAudioSourceFactory : IAudioSourceFactory
{
    public AudioSourceBase? CreateSystemAudioSource()
    {
        var source = LoopbackAudioSource.Default;
        return string.IsNullOrEmpty(source?.DeviceName) ? null : source;
    }

    public AudioSourceBase? CreateMicrophoneSource()
    {
        var source = CaptureAudioSource.Default;
        return string.IsNullOrEmpty(source?.DeviceName) ? null : source;
    }
}
