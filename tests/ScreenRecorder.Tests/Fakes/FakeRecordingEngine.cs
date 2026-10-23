using ScreenRecorder.Core.Abstractions;
using ScreenRecorder.Core.Models;

namespace ScreenRecorder.Tests.Fakes;

internal sealed class FakeRecordingEngine : IRecordingEngine
{
    public event EventHandler<RecordingEngineCompletedEventArgs>? Completed;

    public event EventHandler<RecordingEngineFailedEventArgs>? Failed;

    public RecordingOptions? StartedWith { get; private set; }

    public int StartCalls { get; private set; }

    public int PauseCalls { get; private set; }

    public int ResumeCalls { get; private set; }

    public int StopCalls { get; private set; }

    public Exception? ThrowOnStart { get; set; }

    public void Start(RecordingOptions options)
    {
        StartCalls++;
        if (ThrowOnStart is not null)
        {
            throw ThrowOnStart;
        }

        StartedWith = options;
    }

    public void Pause() => PauseCalls++;

    public void Resume() => ResumeCalls++;

    public void Stop() => StopCalls++;

    public void RaiseCompleted(string path) => Completed?.Invoke(this, new RecordingEngineCompletedEventArgs(path));

    public void RaiseFailed(string message) => Failed?.Invoke(this, new RecordingEngineFailedEventArgs(message));

    public void Dispose()
    {
    }
}
