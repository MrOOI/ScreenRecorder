using ScreenRecorder.Core.Models;

namespace ScreenRecorder.App.ViewModels;

public enum SourceOptionKind
{
    /// <summary>A ready-to-record source.</summary>
    Source,

    /// <summary>Opens the window picker when selected.</summary>
    PickWindow,

    /// <summary>Opens the region selector when selected.</summary>
    PickRegion,
}

/// <summary>
/// One entry of the source drop-down.
/// </summary>
public sealed record SourceOption(string Label, SourceOptionKind Kind, CaptureSource? Source = null)
{
    private const int MaxTitleLength = 32;

    public static SourceOption PickWindow { get; } = new("Window…", SourceOptionKind.PickWindow);

    public static SourceOption PickRegion { get; } = new("Region…", SourceOptionKind.PickRegion);

    public bool IsPicker => Kind != SourceOptionKind.Source;

    /// <summary>Screen readers and default templates show the label.</summary>
    public override string ToString() => Label;

    public static SourceOption ForDisplay(DisplayInfo display, int displayCount) => new(
        displayCount == 1
            ? "Full screen"
            : $"Display {display.Index}{(display.IsPrimary ? " (main)" : string.Empty)} · {display.Bounds}",
        SourceOptionKind.Source,
        new DisplayCaptureSource(display));

    public static SourceOption ForPicked(CaptureSource source, int displayCount) => source switch
    {
        WindowCaptureSource w => new SourceOption($"Window · {Truncate(w.Window.Title)}", SourceOptionKind.Source, source),
        RegionCaptureSource r => new SourceOption(
            displayCount == 1 ? $"Region · {r.Region}" : $"Region · {r.Region} on display {r.Display.Index}",
            SourceOptionKind.Source,
            source),
        DisplayCaptureSource d => ForDisplay(d.Display, displayCount),
        _ => throw new NotSupportedException(source.GetType().Name),
    };

    private static string Truncate(string text) =>
        text.Length <= MaxTitleLength ? text : string.Concat(text.AsSpan(0, MaxTitleLength - 1), "…");
}
