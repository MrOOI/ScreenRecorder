using Microsoft.Extensions.Logging.Abstractions;
using ScreenRecorder.App.Services;
using ScreenRecorder.App.ViewModels;
using ScreenRecorder.Core.Abstractions;
using ScreenRecorder.Core.Models;
using ScreenRecorder.Tests.Fakes;

namespace ScreenRecorder.Tests.App;

public sealed class SourceSelectorViewModelTests
{
    private readonly ICaptureSourceProvider _sources = Substitute.For<ICaptureSourceProvider>();
    private readonly IRegionPicker _regionPicker = Substitute.For<IRegionPicker>();
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly QueuedDispatcher _dispatcher = new();

    [Fact]
    public void Single_display_is_called_full_screen()
    {
        _sources.GetDisplays().Returns([TestData.PrimaryDisplay]);

        var sut = Create();

        sut.Options.Select(o => o.Label).Should().Equal("Full screen", "Window…", "Region…");
        sut.SelectedSource.Should().Be(new DisplayCaptureSource(TestData.PrimaryDisplay));
    }

    [Fact]
    public void Multiple_displays_are_numbered_with_main_marked()
    {
        _sources.GetDisplays().Returns([TestData.PrimaryDisplay, TestData.SecondaryDisplay]);

        var sut = Create();

        sut.Options[0].Label.Should().Be("Display 1 (main) · 1920×1080");
        sut.Options[1].Label.Should().Be("Display 2 · 2560×1440");
    }

    [Fact]
    public void Picking_a_region_adds_and_selects_it()
    {
        _sources.GetDisplays().Returns([TestData.PrimaryDisplay]);
        var region = new RegionCaptureSource(TestData.PrimaryDisplay, new ScreenRegion(10, 10, 640, 360));
        _regionPicker.PickRegionAsync().Returns(region);
        var sut = Create();

        sut.Selected = SourceOption.PickRegion;
        _dispatcher.RunAll();

        sut.Selected!.Label.Should().Be("Region · 640×360");
        sut.SelectedSource.Should().Be(region);
        sut.Options.Select(o => o.Label).Should().Equal("Full screen", "Region · 640×360", "Window…", "Region…");
    }

    [Fact]
    public void Cancelled_pick_restores_previous_selection()
    {
        _sources.GetDisplays().Returns([TestData.PrimaryDisplay, TestData.SecondaryDisplay]);
        _dialogs.PickWindow().Returns((WindowInfo?)null);
        var sut = Create();
        sut.Selected = sut.Options[1];

        sut.Selected = SourceOption.PickWindow;
        _dispatcher.RunAll();

        sut.Selected.Should().Be(sut.Options[1]);
    }

    [Fact]
    public void A_new_pick_replaces_the_previous_one()
    {
        _sources.GetDisplays().Returns([TestData.PrimaryDisplay]);
        _dialogs.PickWindow().Returns(
            new WindowInfo(1, "First", "a"),
            new WindowInfo(2, "A very long window title that will not fit in the box", "b"));
        var sut = Create();

        sut.Selected = SourceOption.PickWindow;
        _dispatcher.RunAll();
        sut.Selected = SourceOption.PickWindow;
        _dispatcher.RunAll();

        sut.Options.Should().HaveCount(4);
        sut.Selected!.Label.Should().StartWith("Window · A very long").And.EndWith("…");
    }

    [Fact]
    public void Refresh_keeps_a_picked_source_selected()
    {
        _sources.GetDisplays().Returns([TestData.PrimaryDisplay]);
        _dialogs.PickWindow().Returns(new WindowInfo(1, "Notes", "notepad"));
        var sut = Create();
        sut.Selected = SourceOption.PickWindow;
        _dispatcher.RunAll();

        sut.Refresh();

        sut.Selected!.Label.Should().Be("Window · Notes");
    }

    private SourceSelectorViewModel Create() =>
        new(_sources, _regionPicker, _dialogs, _dispatcher, NullLogger<SourceSelectorViewModel>.Instance);
}
