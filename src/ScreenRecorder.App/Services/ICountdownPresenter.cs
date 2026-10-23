using System.Windows;
using ScreenRecorder.App.Views;

namespace ScreenRecorder.App.Services;

/// <summary>
/// Shows the large pre-recording countdown number.
/// </summary>
public interface ICountdownPresenter
{
    void Show(int secondsRemaining);

    void Hide();
}

public sealed class CountdownPresenter : ICountdownPresenter
{
    private CountdownWindow? _window;

    public void Show(int secondsRemaining)
    {
        _window ??= new CountdownWindow(PlacementReference());
        _window.SecondsRemaining = secondsRemaining;
        if (!_window.IsVisible)
        {
            _window.Show();
        }
    }

    public void Hide()
    {
        _window?.Close();
        _window = null;
    }

    // Centre on the monitor of the main window: that is where the user just clicked Record.
    private static Window? PlacementReference() => Application.Current?.MainWindow;
}
