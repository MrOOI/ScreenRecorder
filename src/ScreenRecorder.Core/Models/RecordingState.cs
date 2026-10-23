namespace ScreenRecorder.Core.Models;

/// <summary>
/// Lifecycle of a recording session.
/// </summary>
public enum RecordingState
{
    /// <summary>Nothing is happening; a recording can be started.</summary>
    Idle,

    /// <summary>The pre-recording countdown is running.</summary>
    Countdown,

    /// <summary>Frames are being captured.</summary>
    Recording,

    /// <summary>Capture is suspended; elapsed time is frozen.</summary>
    Paused,

    /// <summary>Stop was requested; the file is being finalized.</summary>
    Finishing,

    /// <summary>The last recording failed. A new recording can be started.</summary>
    Failed,
}

public static class RecordingStateExtensions
{
    /// <summary>True while a recording occupies the engine, countdown included.</summary>
    public static bool IsActive(this RecordingState state) =>
        state is RecordingState.Countdown or RecordingState.Recording or RecordingState.Paused or RecordingState.Finishing;

    /// <summary>True when a new recording may be started.</summary>
    public static bool CanStart(this RecordingState state) =>
        state is RecordingState.Idle or RecordingState.Failed;
}
