using ScreenRecorder.Core.Models;

namespace ScreenRecorder.Core.Abstractions;

/// <summary>
/// The single seam between the application and the capture/encode technology.
/// Implementations may raise events on any thread.
/// </summary>
public interface IRecordingEngine : IDisposable
{
    /// <summary>Raised once the output file has been fully written.</summary>
    event EventHandler<RecordingEngineCompletedEventArgs>? Completed;

    /// <summary>Raised when capture or encoding fails; no further events follow for that recording.</summary>
    event EventHandler<RecordingEngineFailedEventArgs>? Failed;

    /// <summary>Begins capturing. Returns immediately; capture runs in the background.</summary>
    /// <exception cref="InvalidOperationException">A recording is already running.</exception>
    void Start(RecordingOptions options);

    void Pause();

    void Resume();

    /// <summary>Requests the recording to finish. <see cref="Completed"/> or <see cref="Failed"/> follows.</summary>
    void Stop();
}

public sealed class RecordingEngineCompletedEventArgs(string filePath) : EventArgs
{
    public string FilePath { get; } = filePath;
}

public sealed class RecordingEngineFailedEventArgs(string errorMessage) : EventArgs
{
    /// <summary>A message suitable for the user.</summary>
    public string ErrorMessage { get; } = errorMessage;
}
