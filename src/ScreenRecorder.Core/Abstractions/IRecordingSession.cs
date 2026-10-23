using ScreenRecorder.Core.Models;

namespace ScreenRecorder.Core.Abstractions;

/// <summary>
/// Orchestrates one recording at a time: countdown, state machine and elapsed time.
/// Events may be raised on any thread; UI consumers must marshal them.
/// </summary>
public interface IRecordingSession
{
    RecordingState State { get; }

    /// <summary>Recorded time so far, excluding pauses.</summary>
    TimeSpan Elapsed { get; }

    event EventHandler<RecordingStateChangedEventArgs>? StateChanged;

    /// <summary>Raised once per second during the countdown with the seconds remaining.</summary>
    event EventHandler<CountdownTickEventArgs>? CountdownTick;

    /// <summary>Raised when a recording ends, successfully or not.</summary>
    event EventHandler<RecordingFinishedEventArgs>? Finished;

    /// <summary>
    /// Runs the countdown, then starts the engine. Completes once recording has begun,
    /// or when the countdown was cancelled.
    /// </summary>
    /// <exception cref="InvalidOperationException">The current state does not allow starting.</exception>
    Task StartAsync(RecordingOptions options, TimeSpan countdown, CancellationToken cancellationToken = default);

    void Pause();

    void Resume();

    /// <summary>Stops the recording, or cancels the countdown if it is still running.</summary>
    void Stop();
}

public sealed class RecordingStateChangedEventArgs(RecordingState oldState, RecordingState newState) : EventArgs
{
    public RecordingState OldState { get; } = oldState;

    public RecordingState NewState { get; } = newState;
}

public sealed class CountdownTickEventArgs(int secondsRemaining) : EventArgs
{
    public int SecondsRemaining { get; } = secondsRemaining;
}

public sealed class RecordingFinishedEventArgs(RecordingResult result) : EventArgs
{
    public RecordingResult Result { get; } = result;
}
