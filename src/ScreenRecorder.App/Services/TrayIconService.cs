using System.Windows;
using H.NotifyIcon;
using ScreenRecorder.App.ViewModels;

namespace ScreenRecorder.App.Services;

/// <summary>
/// Owns the notification-area icon's lifetime. The icon itself is declared in Views/TrayIcon.xaml.
/// </summary>
public sealed class TrayIconService(TrayViewModel viewModel) : IDisposable
{
    private TaskbarIcon? _icon;

    public void Show()
    {
        var resources = new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/Views/TrayIcon.xaml", UriKind.Absolute),
        };

        _icon = (TaskbarIcon)resources["TrayIcon"];
        _icon.DataContext = viewModel;

        // Required because the icon is not part of any window's visual tree.
        _icon.ForceCreate(enablesEfficiencyMode: false);
    }

    public void Dispose()
    {
        _icon?.Dispose();
        _icon = null;
    }
}
