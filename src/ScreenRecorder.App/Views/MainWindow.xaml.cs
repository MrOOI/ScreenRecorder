using System.ComponentModel;
using System.Windows;
using ScreenRecorder.App.ViewModels;

namespace ScreenRecorder.App.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
    }

    /// <summary>
    /// Closing while a recording is active hides to the tray instead, so the recording is never cut off.
    /// </summary>
    protected override void OnClosing(CancelEventArgs e)
    {
        if (_viewModel.IsActive)
        {
            e.Cancel = true;
            Hide();
        }

        base.OnClosing(e);
    }
}
