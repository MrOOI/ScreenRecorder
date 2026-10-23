using System.Windows;
using System.Windows.Input;
using ScreenRecorder.App.ViewModels;

namespace ScreenRecorder.App.Views;

public partial class WindowPickerWindow : Window
{
    private readonly WindowPickerViewModel _viewModel;

    public WindowPickerWindow(WindowPickerViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        viewModel.CloseRequested += OnCloseRequested;
        InitializeComponent();
        Loaded += (_, _) => WindowList.Focus();
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.CloseRequested -= OnCloseRequested;
        base.OnClosed(e);
    }

    private void OnCloseRequested(object? sender, bool confirmed) => DialogResult = confirmed;

    private void OnListDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel.ConfirmCommand.CanExecute(null))
        {
            _viewModel.ConfirmCommand.Execute(null);
        }
    }
}
