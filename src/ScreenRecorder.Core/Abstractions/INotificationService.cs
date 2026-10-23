using ScreenRecorder.Core.Models;

namespace ScreenRecorder.Core.Abstractions;

/// <summary>
/// Tells the user about outcomes they did not directly wait for.
/// </summary>
public interface INotificationService
{
    void ShowRecordingResult(RecordingResult result);

    void ShowError(string message);
}
