namespace ScreenRecorder.Core.Models;

/// <summary>
/// Everything the recording engine needs to produce one video file.
/// </summary>
public sealed record RecordingOptions(
    CaptureSource Source,
    string OutputFilePath,
    bool CaptureSystemAudio,
    bool CaptureMicrophone,
    bool ShowCursor,
    int FrameRate,
    VideoQuality Quality);
