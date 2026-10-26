using Microsoft.Extensions.Logging;
using ScreenRecorder.Core.Abstractions;
using ScreenRecorder.Core.Models;
using ScreenRecorderLib;

namespace ScreenRecorder.Infrastructure.Recording;

/// <summary>
/// <see cref="IRecordingEngine"/> backed by ScreenRecorderLib (DXGI Desktop Duplication / Graphics Capture
/// plus Media Foundation H.264/AAC). A fresh native recorder is created for every recording.
/// </summary>
internal sealed class ScreenRecorderLibEngine(RecordingOptionsMapper mapper, ILogger<ScreenRecorderLibEngine> logger)
    : IRecordingEngine
{
    private readonly object _gate = new();
    private Recorder? _recorder;
    private RecordingOptions? _currentOptions;
    private bool _disposed;

    public event EventHandler<RecordingEngineCompletedEventArgs>? Completed;

    public event EventHandler<RecordingEngineFailedEventArgs>? Failed;

    public void Start(RecordingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_recorder is not null)
            {
                throw new InvalidOperationException("A recording is already in progress.");
            }

            // Failures propagate to the session, which reports them with the options attached.
            StartRecorder(options);
            _currentOptions = options;
        }
    }

    public void Pause() => WithRecorder(r => r.Pause());

    public void Resume() => WithRecorder(r => r.Resume());

    public void Stop() => WithRecorder(r => r.Stop());

    public void Dispose()
    {
        Recorder? recorder;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            recorder = _recorder;
            _recorder = null;
        }

        if (recorder is not null)
        {
            Detach(recorder);
            recorder.Stop();
            recorder.Dispose();
        }
    }

    private void StartRecorder(RecordingOptions options)
    {
        var recorder = Recorder.CreateRecorder(mapper.Map(options));
        recorder.OnRecordingComplete += OnRecordingComplete;
        recorder.OnRecordingFailed += OnRecordingFailed;
        _recorder = recorder;

        logger.LogInformation("Recording {Source} to {Path} at {Fps} fps", options.Source, options.OutputFilePath, options.FrameRate);
        try
        {
            recorder.Record(options.OutputFilePath);
        }
        catch
        {
            _recorder = null;
            Detach(recorder);
            recorder.Dispose();
            throw;
        }
    }

    private void OnRecordingComplete(object? sender, RecordingCompleteEventArgs e)
    {
        logger.LogInformation("Recording saved to {Path}", e.FilePath);
        Release(sender);
        Completed?.Invoke(this, new RecordingEngineCompletedEventArgs(e.FilePath));
    }

    private void OnRecordingFailed(object? sender, RecordingFailedEventArgs e)
    {
        // The native library reports a message, not an exception; wrap it so the failure gets a full report.
        var failure = new RecordingEngineException(e.Error, e.FilePath);
        logger.LogError(failure, "Recording failed: {Error}. Options: {Options}", e.Error, _currentOptions);
        Release(sender);
        Failed?.Invoke(this, new RecordingEngineFailedEventArgs($"The recording failed: {e.Error}"));
    }

    private void WithRecorder(Action<Recorder> action)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_recorder is null)
            {
                throw new InvalidOperationException("No recording is in progress.");
            }

            action(_recorder);
        }
    }

    private void Release(object? sender)
    {
        if (sender is not Recorder recorder)
        {
            return;
        }

        lock (_gate)
        {
            if (ReferenceEquals(_recorder, recorder))
            {
                _recorder = null;
                _currentOptions = null;
            }
        }

        Detach(recorder);

        // Disposing inside the recorder's own callback can deadlock its worker thread.
        _ = Task.Run(recorder.Dispose);
    }

    private void Detach(Recorder recorder)
    {
        recorder.OnRecordingComplete -= OnRecordingComplete;
        recorder.OnRecordingFailed -= OnRecordingFailed;
    }
}
