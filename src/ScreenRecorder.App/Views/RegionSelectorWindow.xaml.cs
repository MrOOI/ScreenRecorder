using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ScreenRecorder.App.Interop;
using ScreenRecorder.App.ViewModels;
using ScreenRecorder.Core.Models;

namespace ScreenRecorder.App.Views;

/// <summary>
/// A borderless overlay covering exactly one display. Input is forwarded to the view model;
/// this class only handles placement and pointer capture.
/// </summary>
public partial class RegionSelectorWindow : Window
{
    private readonly DisplayInfo _display;
    private readonly RegionSelectorViewModel _viewModel;

    public RegionSelectorWindow(DisplayInfo display, RegionSelectorViewModel viewModel)
    {
        _display = display;
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
        SizeChanged += (_, _) => OnSurfaceResized();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        WindowInterop.SetPhysicalBounds(this, _display.Bounds);
        UpdateScale();
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);

        // WPF resizes on DPI change; snap back to the monitor's exact physical bounds.
        WindowInterop.SetPhysicalBounds(this, _display.Bounds);
        UpdateScale();
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        CaptureMouse();
        _viewModel.Begin(e.GetPosition(this));
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        _viewModel.Update(e.GetPosition(this));
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        ReleaseMouseCapture();
        _viewModel.End(e.GetPosition(this));
    }

    protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseRightButtonUp(e);
        _viewModel.Cancel();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape)
        {
            _viewModel.Cancel();
        }
    }

    private void UpdateScale() => _viewModel.Scale = VisualTreeHelper.GetDpi(this).DpiScaleX;

    private void OnSurfaceResized()
    {
        _viewModel.Bounds = new Rect(0, 0, ActualWidth, ActualHeight);

        Hint.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(Hint, (ActualWidth - Hint.DesiredSize.Width) / 2);
        Canvas.SetTop(Hint, (ActualHeight - Hint.DesiredSize.Height) / 2);
    }
}
