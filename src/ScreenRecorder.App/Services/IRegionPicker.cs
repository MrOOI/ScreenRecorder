using ScreenRecorder.App.ViewModels;
using ScreenRecorder.App.Views;
using ScreenRecorder.Core.Abstractions;
using ScreenRecorder.Core.Models;

namespace ScreenRecorder.App.Services;

/// <summary>
/// Lets the user drag out a rectangle on any display.
/// </summary>
public interface IRegionPicker
{
    /// <summary>Returns the selected region, or null when cancelled.</summary>
    Task<RegionCaptureSource?> PickRegionAsync();
}

/// <summary>
/// Shows one overlay per display. Each overlay covers exactly one monitor, which keeps
/// mixed-DPI setups correct and matches the engine's one-display-per-region model.
/// </summary>
public sealed class RegionPicker(ICaptureSourceProvider sources) : IRegionPicker
{
    public Task<RegionCaptureSource?> PickRegionAsync()
    {
        var completion = new TaskCompletionSource<RegionCaptureSource?>();
        var overlays = new List<RegionSelectorWindow>();

        void Complete(RegionCaptureSource? result)
        {
            if (!completion.TrySetResult(result))
            {
                return;
            }

            foreach (var overlay in overlays)
            {
                overlay.Close();
            }
        }

        foreach (var display in sources.GetDisplays())
        {
            var viewModel = new RegionSelectorViewModel();
            var overlay = new RegionSelectorWindow(display, viewModel);
            viewModel.Completed += (_, region) =>
                Complete(region is { } r ? new RegionCaptureSource(display, r) : null);
            overlays.Add(overlay);
        }

        if (overlays.Count == 0)
        {
            return Task.FromResult<RegionCaptureSource?>(null);
        }

        foreach (var overlay in overlays)
        {
            overlay.Show();
        }

        overlays[0].Activate();
        return completion.Task;
    }
}
