using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScreenRecorder.App.Services;
using ScreenRecorder.Core.Abstractions;
using ScreenRecorder.Core.Models;

namespace ScreenRecorder.App.ViewModels;

/// <summary>
/// Edits a copy of the settings; nothing is applied until Save.
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settings;
    private readonly IFolderPicker _folderPicker;

    [ObservableProperty]
    private string _outputFolder;

    [ObservableProperty]
    private int _frameRate;

    [ObservableProperty]
    private VideoQuality _quality;

    [ObservableProperty]
    private bool _countdownEnabled;

    [ObservableProperty]
    private bool _startMinimized;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ErrorMessage))]
    private Hotkey _startStopHotkey;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ErrorMessage))]
    private Hotkey _pauseResumeHotkey;

    public SettingsViewModel(ISettingsService settings, IFolderPicker folderPicker)
    {
        _settings = settings;
        _folderPicker = folderPicker;

        var current = settings.Current;
        _outputFolder = current.OutputFolder;
        _frameRate = current.FrameRate;
        _quality = current.Quality;
        _countdownEnabled = current.CountdownEnabled;
        _startMinimized = current.StartMinimized;
        _startStopHotkey = current.StartStopHotkey;
        _pauseResumeHotkey = current.PauseResumeHotkey;
    }

    /// <summary>Raised with true after a successful save, false on cancel.</summary>
    public event EventHandler<bool>? CloseRequested;

    public IReadOnlyList<int> FrameRates => AppSettings.SupportedFrameRates;

    public IReadOnlyList<VideoQuality> Qualities { get; } = Enum.GetValues<VideoQuality>();

    /// <summary>A validation message, or null when the settings can be saved.</summary>
    public string? ErrorMessage =>
        StartStopHotkey == PauseResumeHotkey ? "The two shortcuts must be different." : null;

    [RelayCommand]
    private void BrowseOutputFolder()
    {
        if (_folderPicker.PickFolder(OutputFolder) is { } folder)
        {
            OutputFolder = folder;
        }
    }

    [RelayCommand]
    private void Save()
    {
        if (ErrorMessage is not null)
        {
            return;
        }

        _settings.Update(s => s with
        {
            OutputFolder = OutputFolder,
            FrameRate = FrameRate,
            Quality = Quality,
            CountdownEnabled = CountdownEnabled,
            StartMinimized = StartMinimized,
            StartStopHotkey = StartStopHotkey,
            PauseResumeHotkey = PauseResumeHotkey,
        });
        CloseRequested?.Invoke(this, true);
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(this, false);
}
