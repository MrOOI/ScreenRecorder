using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScreenRecorder.Core.Abstractions;
using ScreenRecorder.Core.Models;

namespace ScreenRecorder.App.ViewModels;

public sealed partial class WindowPickerViewModel : ObservableObject
{
    private readonly ICaptureSourceProvider _sources;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    private WindowInfo? _selected;

    public WindowPickerViewModel(ICaptureSourceProvider sources)
    {
        _sources = sources;
        Refresh();
    }

    /// <summary>Raised with true when a window was chosen, false on cancel.</summary>
    public event EventHandler<bool>? CloseRequested;

    public ObservableCollection<WindowInfo> Windows { get; } = [];

    public bool IsEmpty => Windows.Count == 0;

    [RelayCommand]
    private void Refresh()
    {
        var previous = Selected;
        Windows.Clear();
        foreach (var window in _sources.GetWindows())
        {
            Windows.Add(window);
        }

        Selected = Windows.FirstOrDefault(w => w.Handle == previous?.Handle);
        OnPropertyChanged(nameof(IsEmpty));
    }

    private bool CanConfirm() => Selected is not null;

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private void Confirm() => CloseRequested?.Invoke(this, true);

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(this, false);
}
