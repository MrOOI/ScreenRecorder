using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using ScreenRecorder.Core.Models;

namespace ScreenRecorder.App.ViewModels;

/// <summary>
/// Drag-to-select logic for one display overlay. Input arrives in DIPs relative to the
/// overlay; the result is in physical pixels relative to the display.
/// </summary>
public sealed partial class RegionSelectorViewModel : ObservableObject
{
    /// <summary>Smaller drags are treated as accidental clicks.</summary>
    public const int MinimumSize = 16;

    private Point _anchor;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowHint))]
    private bool _isSelecting;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SizeText), nameof(ShowHint))]
    private Rect _selection;

    [ObservableProperty]
    private Rect _bounds;

    /// <summary>Raised with the region on success, or null when the user cancelled.</summary>
    public event EventHandler<ScreenRegion?>? Completed;

    /// <summary>Physical pixels per DIP on this overlay's display.</summary>
    public double Scale { get; set; } = 1.0;

    public bool ShowHint => !IsSelecting && Selection.Width <= 0;

    public string SizeText => $"{PhysicalRegion.Width} × {PhysicalRegion.Height}";

    public ScreenRegion PhysicalRegion => ScreenRegion.FromPoints(
        ToPhysical(Selection.Left), ToPhysical(Selection.Top),
        ToPhysical(Selection.Right), ToPhysical(Selection.Bottom)).WithEvenSize();

    public void Begin(Point point)
    {
        _anchor = point;
        IsSelecting = true;
        Selection = new Rect(point, point);
    }

    public void Update(Point point)
    {
        if (IsSelecting)
        {
            Selection = new Rect(_anchor, point);
        }
    }

    public void End(Point point)
    {
        if (!IsSelecting)
        {
            return;
        }

        Update(point);
        IsSelecting = false;

        var region = PhysicalRegion;
        if (region.Width < MinimumSize || region.Height < MinimumSize)
        {
            // Too small to be intentional: let the user try again.
            Selection = default;
            return;
        }

        Completed?.Invoke(this, region);
    }

    public void Cancel() => Completed?.Invoke(this, null);

    private int ToPhysical(double dip) => (int)Math.Round(dip * Scale);
}
