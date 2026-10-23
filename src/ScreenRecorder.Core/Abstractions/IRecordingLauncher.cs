using ScreenRecorder.Core.Models;

namespace ScreenRecorder.Core.Abstractions;

/// <summary>
/// Starts a recording of a source using the user's current settings.
/// </summary>
public interface IRecordingLauncher
{
    /// <summary>
    /// Resolves the output file, builds options from settings and starts the session.
    /// </summary>
    /// <exception cref="OutputFolderUnavailableException">The output folder cannot be used; nothing was started.</exception>
    Task StartAsync(CaptureSource source, CancellationToken cancellationToken = default);
}
