using ScreenRecorder.Core.Abstractions;
using ScreenRecorder.Core.Models;

namespace ScreenRecorder.Core.Services;

/// <summary>
/// Thread-safe state machine around an <see cref="IRecordingEngine"/>.
/// <code>
/// Idle/Failed ─Start─▶ Countdown ─▶ Recording ⇄ Paused
///      ▲                  │ Stop        │ Stop
///      └──────────────────┘             ▼
///      Idle ◀─completed── Finishing ──failed─▶ Failed
/// </code>
/// </summary>
public sealed class RecordingSession : IRecordingSession, IDisposable
{
    /// <summary>
    /// Pause after the countdown so overlays and the main window are gone before the first frame.
    /// </summary>
    public static readonly TimeSpan PreRollDelay = TimeSpan.FromMilliseconds(200);

    private static readonly TimeSpan OneSecond = TimeSpan.FromSeconds(1);

    private readonly IRecordingEngine _engine;
    private readonly TimeProvider _time;
    private readonly object _gate = new();

    private RecordingState _state = RecordingState.Idle;
    private CancellationTokenSource? _countdownCts;
    private TimeSpan _accumulated;
    private long? _segmentStartedAt;

    public RecordingSession(IRecordingEngine engine, TimeProvider time)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _time = time ?? throw new ArgumentNullException(nameof(time));
        _engine.Completed += OnEngineCompleted;
        _engine.Failed += OnEngineFailed;
    }

    public event EventHandler<RecordingStateChangedEventArgs>? StateChanged;

    public event EventHandler<CountdownTickEventArgs>? CountdownTick;

    public event EventHandler<RecordingFinishedEventArgs>? Finished;

    public RecordingState State
    {
        get { lock (_gate) return _state; }
    }

    public TimeSpan Elapsed
    {
        get { lock (_gate) return ElapsedUnsafe(); }
    }

    public async Task StartAsync(RecordingOptions options, TimeSpan countdown, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        CancellationTokenSource cts;
        RecordingStateChangedEventArgs? change;
        lock (_gate)
        {
            if (!_state.CanStart())
            {
                throw new InvalidOperationException($"Cannot start a recording while {_state}.");
            }

            cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _countdownCts = cts;
            _accumulated = TimeSpan.Zero;
            _segmentStartedAt = null;
            change = SetState(RecordingState.Countdown);
        }

        Raise(change);

        try
        {
            await RunCountdownAsync(countdown, cts.Token).ConfigureAwait(false);
            BeginCapture(options, cts);
        }
        catch (OperationCanceledException)
        {
            CancelCountdown();
        }
        finally
        {
            lock (_gate)
            {
                if (ReferenceEquals(_countdownCts, cts))
                {
                    _countdownCts = null;
                }
            }

            cts.Dispose();
        }
    }

    public void Pause()
    {
        RecordingStateChangedEventArgs? change;
        lock (_gate)
        {
            EnsureState(RecordingState.Recording, nameof(Pause));
            _engine.Pause();
            CloseSegment();
            change = SetState(RecordingState.Paused);
        }

        Raise(change);
    }

    public void Resume()
    {
        RecordingStateChangedEventArgs? change;
        lock (_gate)
        {
            EnsureState(RecordingState.Paused, nameof(Resume));
            _engine.Resume();
            _segmentStartedAt = _time.GetTimestamp();
            change = SetState(RecordingState.Recording);
        }

        Raise(change);
    }

    public void Stop()
    {
        RecordingStateChangedEventArgs? change = null;
        lock (_gate)
        {
            switch (_state)
            {
                case RecordingState.Countdown:
                    // StartAsync observes the cancellation and returns to Idle.
                    _countdownCts?.Cancel();
                    return;
                case RecordingState.Finishing:
                    return;
                case RecordingState.Recording or RecordingState.Paused:
                    CloseSegment();
                    change = SetState(RecordingState.Finishing);
                    break;
                default:
                    throw new InvalidOperationException($"Cannot stop while {_state}.");
            }
        }

        // Announce Finishing before the engine can report completion, so subscribers see events in order.
        Raise(change);
        _engine.Stop();
    }

    public void Dispose()
    {
        _engine.Completed -= OnEngineCompleted;
        _engine.Failed -= OnEngineFailed;
        lock (_gate)
        {
            _countdownCts?.Cancel();
        }
    }

    private async Task RunCountdownAsync(TimeSpan countdown, CancellationToken token)
    {
        var seconds = (int)Math.Ceiling(Math.Max(0, countdown.TotalSeconds));
        for (var remaining = seconds; remaining > 0; remaining--)
        {
            CountdownTick?.Invoke(this, new CountdownTickEventArgs(remaining));
            await Task.Delay(OneSecond, _time, token).ConfigureAwait(false);
        }

        CountdownTick?.Invoke(this, new CountdownTickEventArgs(0));
        await Task.Delay(PreRollDelay, _time, token).ConfigureAwait(false);
    }

    private void BeginCapture(RecordingOptions options, CancellationTokenSource cts)
    {
        RecordingStateChangedEventArgs? change;
        RecordingResult? failure = null;
        lock (_gate)
        {
            // Stop may have arrived after the last delay completed.
            cts.Token.ThrowIfCancellationRequested();
            if (_state != RecordingState.Countdown)
            {
                return;
            }

            try
            {
                _engine.Start(options);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                failure = RecordingResult.Failure(
                    $"The recording could not start. {ex.Message}",
                    TimeSpan.Zero,
                    new RecordingStartException(options, ex));
            }

            // The engine may already have failed synchronously inside Start.
            if (_state != RecordingState.Countdown)
            {
                return;
            }

            if (failure is null)
            {
                _segmentStartedAt = _time.GetTimestamp();
            }

            change = SetState(failure is null ? RecordingState.Recording : RecordingState.Failed);
        }

        Raise(change);
        if (failure is not null)
        {
            Finished?.Invoke(this, new RecordingFinishedEventArgs(failure));
        }
    }

    private void CancelCountdown()
    {
        RecordingStateChangedEventArgs? change = null;
        lock (_gate)
        {
            if (_state == RecordingState.Countdown)
            {
                change = SetState(RecordingState.Idle);
            }
        }

        Raise(change);
    }

    private void OnEngineCompleted(object? sender, RecordingEngineCompletedEventArgs e) =>
        Finish(RecordingState.Idle, duration => RecordingResult.Success(e.FilePath, duration));

    private void OnEngineFailed(object? sender, RecordingEngineFailedEventArgs e) =>
        Finish(RecordingState.Failed, duration => RecordingResult.Failure(e.ErrorMessage, duration));

    private void Finish(RecordingState finalState, Func<TimeSpan, RecordingResult> createResult)
    {
        RecordingStateChangedEventArgs? change;
        RecordingResult result;
        lock (_gate)
        {
            // Countdown is included: an engine may fail synchronously inside Start.
            if (_state is not (RecordingState.Countdown or RecordingState.Recording
                or RecordingState.Paused or RecordingState.Finishing))
            {
                return;
            }

            CloseSegment();
            result = createResult(_accumulated);
            change = SetState(finalState);
        }

        Raise(change);
        Finished?.Invoke(this, new RecordingFinishedEventArgs(result));
    }

    private TimeSpan ElapsedUnsafe() =>
        _segmentStartedAt is { } start ? _accumulated + _time.GetElapsedTime(start) : _accumulated;

    private void CloseSegment()
    {
        _accumulated = ElapsedUnsafe();
        _segmentStartedAt = null;
    }

    private void EnsureState(RecordingState required, string operation)
    {
        if (_state != required)
        {
            throw new InvalidOperationException($"Cannot {operation.ToLowerInvariant()} while {_state}.");
        }
    }

    private RecordingStateChangedEventArgs? SetState(RecordingState newState)
    {
        var oldState = _state;
        if (oldState == newState)
        {
            return null;
        }

        _state = newState;
        return new RecordingStateChangedEventArgs(oldState, newState);
    }

    private void Raise(RecordingStateChangedEventArgs? change)
    {
        if (change is not null)
        {
            StateChanged?.Invoke(this, change);
        }
    }
}
