using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using ScreenRecorder.App.Services;
using ScreenRecorder.Core.Abstractions;
using ScreenRecorder.Core.Models;

namespace ScreenRecorder.App.ViewModels;

/// <summary>
/// The source drop-down: displays, plus "Window…" and "Region…" entries that open pickers.
/// A picked window or region becomes a regular entry so it can be recorded again.
/// </summary>
public sealed partial class SourceSelectorViewModel : ObservableObject
{
    private readonly ICaptureSourceProvider _sources;
    private readonly IRegionPicker _regionPicker;
    private readonly IDialogService _dialogs;
    private readonly IUiDispatcher _dispatcher;
    private readonly ILogger<SourceSelectorViewModel> _logger;
    private SourceOption? _picked;
    private int _displayCount;

    [ObservableProperty]
    private SourceOption? _selected;

    public SourceSelectorViewModel(
        ICaptureSourceProvider sources,
        IRegionPicker regionPicker,
        IDialogService dialogs,
        IUiDispatcher dispatcher,
        ILogger<SourceSelectorViewModel> logger)
    {
        _sources = sources;
        _regionPicker = regionPicker;
        _dialogs = dialogs;
        _dispatcher = dispatcher;
        _logger = logger;
        Refresh();
    }

    public ObservableCollection<SourceOption> Options { get; } = [];

    /// <summary>The source to record, or null while a picker entry is selected.</summary>
    public CaptureSource? SelectedSource => Selected?.Source;

    /// <summary>Re-reads displays, e.g. after a monitor was connected.</summary>
    public void Refresh()
    {
        var previous = Selected;
        var displays = _sources.GetDisplays();
        _displayCount = displays.Count;

        Options.Clear();
        foreach (var display in displays)
        {
            Options.Add(SourceOption.ForDisplay(display, _displayCount));
        }

        if (_picked is not null)
        {
            Options.Add(_picked);
        }

        Options.Add(SourceOption.PickWindow);
        Options.Add(SourceOption.PickRegion);

        Selected = Options.FirstOrDefault(o => o == previous && !o.IsPicker) ?? Options.FirstOrDefault();
    }

    partial void OnSelectedChanged(SourceOption? oldValue, SourceOption? newValue)
    {
        OnPropertyChanged(nameof(SelectedSource));
        if (newValue is { IsPicker: true })
        {
            // Deferred: changing the selection inside its own change notification confuses the ComboBox.
            _dispatcher.Post(() => _ = PickAsync(newValue.Kind, oldValue));
        }
    }

    private async Task PickAsync(SourceOptionKind kind, SourceOption? previous)
    {
        try
        {
            CaptureSource? picked = kind switch
            {
                SourceOptionKind.PickWindow => _dialogs.PickWindow() is { } w ? new WindowCaptureSource(w) : null,
                SourceOptionKind.PickRegion => await _regionPicker.PickRegionAsync(),
                _ => null,
            };

            if (picked is null)
            {
                Selected = previous is { IsPicker: false } ? previous : Options.FirstOrDefault();
                return;
            }

            ReplacePicked(SourceOption.ForPicked(picked, _displayCount));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _logger.LogError(ex, "Picking a {Kind} source failed", kind);
            Selected = Options.FirstOrDefault();
        }
    }

    private void ReplacePicked(SourceOption option)
    {
        if (_picked is not null)
        {
            Options.Remove(_picked);
        }

        _picked = option;
        Options.Insert(Options.IndexOf(SourceOption.PickWindow), option);
        Selected = option;
    }
}
