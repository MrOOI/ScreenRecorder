using ScreenRecorder.Core.Abstractions;
using ScreenRecorder.Core.Models;

namespace ScreenRecorder.Core.Services;

public sealed class RecordingLauncher(
    IRecordingSession session,
    ISettingsService settings,
    IOutputPathResolver paths) : IRecordingLauncher
{
    public Task StartAsync(CaptureSource source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        var current = settings.Current;

        // Resolved before the countdown so a bad folder is reported immediately.
        var filePath = paths.ResolveNewFilePath(current.OutputFolder);

        var options = new RecordingOptions(
            source,
            filePath,
            current.CaptureSystemAudio,
            current.CaptureMicrophone,
            current.ShowCursor,
            current.FrameRate,
            current.Quality);

        return session.StartAsync(options, current.EffectiveCountdown, cancellationToken);
    }
}
