using System.Windows;
using ScreenRecorder.App.Views;

namespace ScreenRecorder.App.Services;

/// <summary>
/// Controls main window visibility so it never appears in a recording.
/// </summary>
public interface IAppWindowService
{
    void Hide();

    /// <summary>Shows, un-minimizes and activates the main window.</summary>
    void Restore();
}

public sealed class MainWindowService(MainWindow window) : IAppWindowService
{
    public void Hide() => window.Hide();

    public void Restore()
    {
        if (!window.IsVisible)
        {
            window.Show();
        }

        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }

        window.Activate();
    }
}
