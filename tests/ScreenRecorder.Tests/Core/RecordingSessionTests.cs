using Microsoft.Extensions.Time.Testing;
using ScreenRecorder.Core.Abstractions;
using ScreenRecorder.Core.Models;
using ScreenRecorder.Core.Services;
using ScreenRecorder.Tests.Fakes;

namespace ScreenRecorder.Tests.Core;

public sealed class RecordingSessionTests : IDisposable
{
    private readonly FakeRecordingEngine _engine = new();
    private readonly FakeTimeProvider _time = new();
    private readonly RecordingSession _sut;
    private readonly List<RecordingState> _states = [];
    private readonly List<RecordingResult> _results = [];

    public RecordingSessionTests()
    {
        _sut = new RecordingSession(_engine, _time);
        _sut.StateChanged += (_, e) => _states.Add(e.NewState);
        _sut.Finished += (_, e) => _results.Add(e.Result);
    }

    public void Dispose() => _sut.Dispose();

    [Fact]
    public void Initial_state_is_idle()
    {
        _sut.State.Should().Be(RecordingState.Idle);
        _sut.Elapsed.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public async Task Start_without_countdown_starts_engine_after_pre_roll()
    {
        var options = TestData.Options();

        var start = _sut.StartAsync(options, TimeSpan.Zero);
        _sut.State.Should().Be(RecordingState.Countdown);
        _engine.StartCalls.Should().Be(0, "the UI needs the pre-roll to get out of the frame");

        await _time.AdvanceUntilAsync(() => start.IsCompleted);
        await start;

        _sut.State.Should().Be(RecordingState.Recording);
        _engine.StartedWith.Should().Be(options);
        _states.Should().Equal(RecordingState.Countdown, RecordingState.Recording);
    }

    [Fact]
    public async Task Countdown_ticks_every_second_down_to_zero()
    {
        var ticks = new List<int>();
        _sut.CountdownTick += (_, e) => ticks.Add(e.SecondsRemaining);

        var start = _sut.StartAsync(TestData.Options(), TimeSpan.FromSeconds(3));
        await _time.AdvanceUntilAsync(() => start.IsCompleted);

        ticks.Should().Equal(3, 2, 1, 0);
        _sut.State.Should().Be(RecordingState.Recording);
    }

    [Fact]
    public async Task Stop_during_countdown_cancels_without_starting_engine()
    {
        var start = _sut.StartAsync(TestData.Options(), TimeSpan.FromSeconds(3));

        _sut.Stop();
        await start;

        _sut.State.Should().Be(RecordingState.Idle);
        _engine.StartCalls.Should().Be(0);
        _results.Should().BeEmpty();
        _states.Should().Equal(RecordingState.Countdown, RecordingState.Idle);
    }

    [Fact]
    public async Task Caller_cancellation_during_countdown_returns_to_idle()
    {
        using var cts = new CancellationTokenSource();
        var start = _sut.StartAsync(TestData.Options(), TimeSpan.FromSeconds(3), cts.Token);

        await cts.CancelAsync();
        await start;

        _sut.State.Should().Be(RecordingState.Idle);
        _engine.StartCalls.Should().Be(0);
    }

    [Fact]
    public async Task Elapsed_excludes_paused_time()
    {
        await StartRecordingAsync();
        var startedAt = _sut.Elapsed;

        _time.Advance(TimeSpan.FromSeconds(5));
        _sut.Pause();
        _time.Advance(TimeSpan.FromSeconds(10));
        _sut.Elapsed.Should().Be(startedAt + TimeSpan.FromSeconds(5), "time is frozen while paused");

        _sut.Resume();
        _time.Advance(TimeSpan.FromSeconds(2));

        _sut.Elapsed.Should().Be(startedAt + TimeSpan.FromSeconds(7));
        _engine.PauseCalls.Should().Be(1);
        _engine.ResumeCalls.Should().Be(1);
    }

    [Fact]
    public async Task Stop_then_engine_completion_produces_success_result()
    {
        await StartRecordingAsync();
        _time.Advance(TimeSpan.FromSeconds(4));

        _sut.Stop();
        _sut.State.Should().Be(RecordingState.Finishing);
        _engine.StopCalls.Should().Be(1);

        _time.Advance(TimeSpan.FromSeconds(30));
        _engine.RaiseCompleted(@"C:\Videos\test.mp4");

        _sut.State.Should().Be(RecordingState.Idle);
        _results.Should().ContainSingle()
            .Which.Should().Match<RecordingResult>(r => r.IsSuccess && r.FilePath == @"C:\Videos\test.mp4");
        _results[0].Duration.Should().BeCloseTo(TimeSpan.FromSeconds(4), TimeSpan.FromMilliseconds(500),
            "finalizing time is not recorded time");
    }

    [Fact]
    public async Task Stop_while_finishing_is_ignored()
    {
        await StartRecordingAsync();
        _sut.Stop();

        _sut.Stop();

        _engine.StopCalls.Should().Be(1);
    }

    [Fact]
    public async Task Engine_failure_moves_to_failed_and_allows_restart()
    {
        await StartRecordingAsync();

        _engine.RaiseFailed("Display lost.");

        _sut.State.Should().Be(RecordingState.Failed);
        _results.Should().ContainSingle().Which.ErrorMessage.Should().Be("Display lost.");

        await StartRecordingAsync();
        _sut.State.Should().Be(RecordingState.Recording);
    }

    [Fact]
    public async Task Engine_start_exception_moves_to_failed_with_message()
    {
        _engine.ThrowOnStart = new InvalidOperationException("No display.");

        var start = _sut.StartAsync(TestData.Options(), TimeSpan.Zero);
        await _time.AdvanceUntilAsync(() => start.IsCompleted);

        _sut.State.Should().Be(RecordingState.Failed);
        _results.Should().ContainSingle().Which.ErrorMessage.Should().Contain("No display.");

        // The cause travels with the result so the app can write an error report.
        var cause = _results[0].Exception.Should().BeOfType<RecordingStartException>().Subject;
        cause.Options.Should().Be(TestData.Options());
        cause.InnerException!.Message.Should().Be("No display.");
    }

    [Fact]
    public void Engine_events_while_idle_are_ignored()
    {
        _engine.RaiseCompleted("x.mp4");
        _engine.RaiseFailed("boom");

        _sut.State.Should().Be(RecordingState.Idle);
        _results.Should().BeEmpty();
    }

    [Fact]
    public void Illegal_transitions_from_idle_throw()
    {
        _sut.Invoking(s => s.Pause()).Should().Throw<InvalidOperationException>();
        _sut.Invoking(s => s.Resume()).Should().Throw<InvalidOperationException>();
        _sut.Invoking(s => s.Stop()).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task Illegal_transitions_while_recording_throw()
    {
        await StartRecordingAsync();

        _sut.Invoking(s => s.Resume()).Should().Throw<InvalidOperationException>();
        await _sut.Invoking(s => s.StartAsync(TestData.Options(), TimeSpan.Zero))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Start_resets_elapsed_from_previous_recording()
    {
        await StartRecordingAsync();
        _time.Advance(TimeSpan.FromSeconds(10));
        _sut.Stop();
        _engine.RaiseCompleted("a.mp4");

        await StartRecordingAsync();

        _sut.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(1));
    }

    private async Task StartRecordingAsync()
    {
        var start = _sut.StartAsync(TestData.Options(), TimeSpan.Zero);
        await _time.AdvanceUntilAsync(() => start.IsCompleted);
        await start;
    }
}
