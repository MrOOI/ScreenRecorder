using System.IO;
using ScreenRecorder.Core.Models;

namespace ScreenRecorder.App.ViewModels;

/// <summary>
/// User-facing wording derived from state. Pure functions, kept out of the view models.
/// </summary>
public static class DisplayText
{
    public static string ForStatus(RecordingState state, int countdownRemaining) => state switch
    {
        RecordingState.Countdown when countdownRemaining > 0 => $"Starting in {countdownRemaining}…",
        RecordingState.Countdown => "Starting…",
        RecordingState.Recording => "Recording",
        RecordingState.Paused => "Paused",
        RecordingState.Finishing => "Saving…",
        RecordingState.Failed => "Failed",
        _ => "Ready",
    };

    /// <summary>The last two path segments, e.g. <c>Videos\ScreenRecorder</c>.</summary>
    public static string ForFolder(string folder)
    {
        var trimmed = folder.TrimEnd(Path.DirectorySeparatorChar);
        var parent = Path.GetFileName(Path.GetDirectoryName(trimmed));
        return string.IsNullOrEmpty(parent) ? trimmed : Path.Combine(parent, Path.GetFileName(trimmed));
    }
}
