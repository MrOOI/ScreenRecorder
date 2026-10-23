using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScreenRecorder.App.Services;
using ScreenRecorder.Core.Abstractions;
using ScreenRecorder.Core.Models;

namespace ScreenRecorder.App.ViewModels;

/// <summary>
/// The notification-area icon: mirrors the main commands and owns "Exit", which
/// finishes an active recording before the app closes so the file is never lost.
/// </summary>
public sealed partial class TrayViewModel : ObservableObject, IDisposable
{
    private readonly IAppWindowService _window;
    private readonly IAppLifetime _lifetime;
    private readonly IRecordingSession _session;
    private readonly IUiDispatcher _dispatcher;
    private bool _exitRequested;

    public TrayViewModel(
        MainViewModel main,
        IAppWindowService window,
        IAppLifetime lifetime,
        IRecordingSession session,
        IUiDispatcher dispatcher)
    {
        Main = main;
        _window = window;
        _lifetime = lifetime;
        _session = session;
        _dispatcher = dispatcher;
        Main.PropertyChanged += OnMainPropertyChanged;
        _session.StateChanged += OnSessionStateChanged;
    }

    public MainViewModel Main { get; }

    public string ToolTipText => Main.IsActive
        ? $"Screen Recorder · {Main.StatusText} {Main.ElapsedText}"
        : "Screen Recorder";

    public void Dispose()
    {
        Main.PropertyChanged -= OnMainPropertyChanged;
        _session.StateChanged -= OnSessionStateChanged;
    }

    [RelayCommand]
    private void ShowWindow() => _window.Restore();

    [RelayCommand]
    private void Exit()
    {
        var state = _session.State;
        if (!state.IsActive())
        {
            _lifetime.Shutdown();
            return;
        }

        _exitRequested = true;
        if (state != RecordingState.Finishing)
        {
            _session.Stop();
        }
    }

    private void OnSessionStateChanged(object? sender, RecordingStateChangedEventArgs e) => _dispatcher.Post(() =>
    {
        if (_exitRequested && !e.NewState.IsActive())
        {
            _lifetime.Shutdown();
        }
    });

    private void OnMainPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.StatusText) or nameof(MainViewModel.ElapsedText)
            or nameof(MainViewModel.IsActive))
        {
            OnPropertyChanged(nameof(ToolTipText));
        }
    }
}
