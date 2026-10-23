using ScreenRecorder.Core.Abstractions;
using ScreenRecorder.Core.Models;

namespace ScreenRecorder.App.Services;

/// <summary>
/// Reacts to the recording lifecycle with window-level effects: hides the main window
/// before capture, drives the countdown overlay, restores the window and reports the result.
/// </summary>
public sealed class RecordingUiCoordinator(
    IRecordingSession session,
    IUiDispatcher dispatcher,
    IAppWindowService window,
    ICountdownPresenter countdown,
    INotificationService notifications) : IDisposable
{
    public void Start()
    {
        session.StateChanged += OnStateChanged;
        session.CountdownTick += OnCountdownTick;
        session.Finished += OnFinished;
    }

    public void Dispose()
    {
        session.StateChanged -= OnStateChanged;
        session.CountdownTick -= OnCountdownTick;
        session.Finished -= OnFinished;
    }

    private void OnStateChanged(object? sender, RecordingStateChangedEventArgs e) => dispatcher.Post(() =>
    {
        switch (e.NewState)
        {
            case RecordingState.Countdown:
                window.Hide();
                break;
            case RecordingState.Recording:
                countdown.Hide();
                break;
            case RecordingState.Idle or RecordingState.Failed:
                countdown.Hide();
                window.Restore();
                break;
        }
    });

    private void OnCountdownTick(object? sender, CountdownTickEventArgs e) => dispatcher.Post(() =>
    {
        if (e.SecondsRemaining > 0)
        {
            countdown.Show(e.SecondsRemaining);
        }
        else
        {
            countdown.Hide();
        }
    });

    private void OnFinished(object? sender, RecordingFinishedEventArgs e) =>
        notifications.ShowRecordingResult(e.Result);
}
