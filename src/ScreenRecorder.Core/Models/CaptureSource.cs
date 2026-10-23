namespace ScreenRecorder.Core.Models;

/// <summary>
/// What to record. A closed hierarchy: a display, a window, or a region of a display.
/// </summary>
public abstract record CaptureSource
{
    private protected CaptureSource()
    {
    }
}

/// <summary>Records an entire display.</summary>
public sealed record DisplayCaptureSource(DisplayInfo Display) : CaptureSource;

/// <summary>Records a single application window.</summary>
public sealed record WindowCaptureSource(WindowInfo Window) : CaptureSource;

/// <summary>Records a rectangular region of one display.</summary>
/// <param name="Display">The display containing the region.</param>
/// <param name="Region">The region relative to the display's top-left corner, in physical pixels.</param>
public sealed record RegionCaptureSource(DisplayInfo Display, ScreenRegion Region) : CaptureSource;
