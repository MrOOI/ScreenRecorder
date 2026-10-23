using System.Windows;
using ScreenRecorder.App.Interop;

namespace ScreenRecorder.App.Views;

/// <summary>
/// Large click-through countdown, excluded from capture so it can never leak into a recording.
/// </summary>
public partial class CountdownWindow : Window
{
    public static readonly DependencyProperty SecondsRemainingProperty = DependencyProperty.Register(
        nameof(SecondsRemaining), typeof(int), typeof(CountdownWindow), new PropertyMetadata(0));

    private readonly Window? _placementReference;

    public CountdownWindow(Window? placementReference)
    {
        _placementReference = placementReference;
        InitializeComponent();
    }

    public int SecondsRemaining
    {
        get => (int)GetValue(SecondsRemainingProperty);
        set => SetValue(SecondsRemainingProperty, value);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        WindowInterop.MakeClickThrough(this);
        WindowInterop.ExcludeFromCapture(this);
        CenterOnReferenceMonitor();
    }

    private void CenterOnReferenceMonitor()
    {
        var area = WindowInterop.GetWorkAreaOf(_placementReference ?? this);
        if (area.IsEmpty)
        {
            return;
        }

        // Size is fixed in XAML; convert it to physical pixels for this monitor.
        var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(this);
        var width = (int)Math.Round(160 * dpi.DpiScaleX);
        var height = (int)Math.Round(160 * dpi.DpiScaleY);
        WindowInterop.SetPhysicalBounds(this, new Core.Models.ScreenRegion(
            area.X + (area.Width - width) / 2,
            area.Y + (area.Height - height) / 2,
            width,
            height));
    }
}
