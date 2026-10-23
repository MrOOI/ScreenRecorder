using System.Windows;
using ScreenRecorder.App.ViewModels;
using ScreenRecorder.App.Views;
using ScreenRecorder.Core.Models;

namespace ScreenRecorder.App.Services;

/// <summary>
/// Opens the application's modal dialogs.
/// </summary>
public interface IDialogService
{
    void ShowSettings();

    /// <summary>Returns the chosen window, or null when cancelled.</summary>
    WindowInfo? PickWindow();
}

public sealed class DialogService(
    Func<SettingsViewModel> settingsViewModelFactory,
    Func<WindowPickerViewModel> windowPickerViewModelFactory,
    HotkeyManager hotkeys) : IDialogService
{
    public void ShowSettings()
    {
        // While the user types a new shortcut, the old one must not trigger a recording.
        using (hotkeys.Suspend())
        {
            var window = new SettingsWindow(settingsViewModelFactory()) { Owner = VisibleMainWindow() };
            window.ShowDialog();
        }
    }

    public WindowInfo? PickWindow()
    {
        var viewModel = windowPickerViewModelFactory();
        var window = new WindowPickerWindow(viewModel) { Owner = VisibleMainWindow() };
        return window.ShowDialog() == true ? viewModel.Selected : null;
    }

    private static Window? VisibleMainWindow() =>
        Application.Current?.MainWindow is { IsVisible: true } main ? main : null;
}
