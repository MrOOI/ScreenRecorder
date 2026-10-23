using ScreenRecorder.App.ViewModels;
using ScreenRecorder.Core.Abstractions;
using ScreenRecorder.Core.Models;

namespace ScreenRecorder.Tests.App;

public sealed class WindowPickerViewModelTests
{
    private static readonly WindowInfo Notepad = new(10, "Notes", "notepad");
    private static readonly WindowInfo Browser = new(20, "Docs", "chrome");

    private readonly ICaptureSourceProvider _sources = Substitute.For<ICaptureSourceProvider>();

    [Fact]
    public void Lists_windows_and_requires_a_selection_to_confirm()
    {
        _sources.GetWindows().Returns([Notepad, Browser]);

        var sut = new WindowPickerViewModel(_sources);

        sut.Windows.Should().Equal(Notepad, Browser);
        sut.IsEmpty.Should().BeFalse();
        sut.ConfirmCommand.CanExecute(null).Should().BeFalse();

        sut.Selected = Browser;
        sut.ConfirmCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public void Refresh_keeps_the_selected_window_when_it_still_exists()
    {
        _sources.GetWindows().Returns([Notepad, Browser], [Browser with { Title = "Docs (2)" }]);
        var sut = new WindowPickerViewModel(_sources) { Selected = Browser };

        sut.RefreshCommand.Execute(null);

        sut.Selected!.Title.Should().Be("Docs (2)");
    }

    [Fact]
    public void Empty_list_is_flagged()
    {
        _sources.GetWindows().Returns([]);

        new WindowPickerViewModel(_sources).IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void Confirm_and_cancel_request_close()
    {
        _sources.GetWindows().Returns([Notepad]);
        var sut = new WindowPickerViewModel(_sources) { Selected = Notepad };
        var results = new List<bool>();
        sut.CloseRequested += (_, confirmed) => results.Add(confirmed);

        sut.ConfirmCommand.Execute(null);
        sut.CancelCommand.Execute(null);

        results.Should().Equal(true, false);
    }
}
