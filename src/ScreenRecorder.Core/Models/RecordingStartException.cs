namespace ScreenRecorder.Core.Models;

/// <summary>
/// A recording could not be started. Carries the options that were used, so a report shows exactly what was attempted.
/// </summary>
public sealed class RecordingStartException(RecordingOptions options, Exception innerException)
    : Exception($"Could not start recording. Options: {options}", innerException)
{
    public RecordingOptions Options { get; } = options;
}
