using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScreenRecorder.App.Services;
using ScreenRecorder.Core.Abstractions;

namespace ScreenRecorder.App.ViewModels;

/// <summary>
/// The inline notice under the record button. Success notices auto-dismiss; errors stay until dismissed.
/// </summary>
public sealed partial class BannerViewModel(IShellService shell, IUiDispatcher dispatcher, TimeProvider time)
    : ObservableObject, IDisposable
{
    public static readonly TimeSpan SuccessDisplayTime = TimeSpan.FromSeconds(8);

    private ITimer? _dismissTimer;
    private int _generation;

    [ObservableProperty]
    private bool _isVisible;

    [ObservableProperty]
    private bool _isError;

    [ObservableProperty]
    private string _message = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFile))]
    [NotifyCanExecuteChangedFor(nameof(OpenFileCommand), nameof(RevealFileCommand))]
    private string? _filePath;

    public bool HasFile => FilePath is not null;

    public void ShowSuccess(string message, string filePath)
    {
        Show(message, isError: false, filePath);

        // A tick already queued for an older notice must not dismiss this one.
        var generation = _generation;
        _dismissTimer = time.CreateTimer(
            _ => dispatcher.Post(() =>
            {
                if (generation == _generation)
                {
                    Dismiss();
                }
            }),
            null,
            SuccessDisplayTime,
            Timeout.InfiniteTimeSpan);
    }

    public void ShowError(string message) => Show(message, isError: true, filePath: null);

    [RelayCommand]
    public void Dismiss()
    {
        StopTimer();
        IsVisible = false;
    }

    public void Dispose() => StopTimer();

    [RelayCommand(CanExecute = nameof(HasFile))]
    private void OpenFile()
    {
        shell.OpenFile(FilePath!);
        Dismiss();
    }

    [RelayCommand(CanExecute = nameof(HasFile))]
    private void RevealFile()
    {
        shell.RevealFile(FilePath!);
        Dismiss();
    }

    private void Show(string message, bool isError, string? filePath)
    {
        StopTimer();
        _generation++;
        Message = message;
        IsError = isError;
        FilePath = filePath;
        IsVisible = true;
    }

    private void StopTimer()
    {
        _dismissTimer?.Dispose();
        _dismissTimer = null;
    }
}
