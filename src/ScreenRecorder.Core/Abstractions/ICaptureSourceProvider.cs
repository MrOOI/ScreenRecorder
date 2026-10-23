using ScreenRecorder.Core.Models;

namespace ScreenRecorder.Core.Abstractions;

/// <summary>
/// Enumerates what can be recorded right now.
/// </summary>
public interface ICaptureSourceProvider
{
    /// <summary>Displays ordered with the primary display first.</summary>
    IReadOnlyList<DisplayInfo> GetDisplays();

    /// <summary>Visible, titled top-level windows of other applications.</summary>
    IReadOnlyList<WindowInfo> GetWindows();
}
