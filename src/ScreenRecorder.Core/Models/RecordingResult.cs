namespace ScreenRecorder.Core.Models;

/// <summary>
/// Outcome of a finished recording.
/// </summary>
public sealed record RecordingResult
{
    private RecordingResult(bool isSuccess, string? filePath, TimeSpan duration, string? errorMessage, Exception? exception)
    {
        IsSuccess = isSuccess;
        FilePath = filePath;
        Duration = duration;
        ErrorMessage = errorMessage;
        Exception = exception;
    }

    public bool IsSuccess { get; }

    /// <summary>The saved file; set on success.</summary>
    public string? FilePath { get; }

    /// <summary>Recorded time, excluding pauses.</summary>
    public TimeSpan Duration { get; }

    /// <summary>A message suitable for the user; set on failure.</summary>
    public string? ErrorMessage { get; }

    /// <summary>
    /// The technical cause, when one was caught and nobody has logged it yet.
    /// Consumers should log it; failures already logged where they happened leave this null.
    /// </summary>
    public Exception? Exception { get; }

    public static RecordingResult Success(string filePath, TimeSpan duration) =>
        new(true, filePath, duration, null, null);

    public static RecordingResult Failure(string errorMessage, TimeSpan duration, Exception? exception = null) =>
        new(false, null, duration, errorMessage, exception);
}
