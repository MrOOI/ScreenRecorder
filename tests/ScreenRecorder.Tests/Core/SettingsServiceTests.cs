using ScreenRecorder.Core.Abstractions;
using ScreenRecorder.Core.Models;
using ScreenRecorder.Core.Services;

namespace ScreenRecorder.Tests.Core;

public sealed class SettingsServiceTests
{
    private readonly ISettingsStore _store = Substitute.For<ISettingsStore>();

    [Fact]
    public void Loads_and_normalizes_on_construction()
    {
        _store.Load().Returns(new AppSettings { FrameRate = 999 });

        var sut = new SettingsService(_store);

        sut.Current.FrameRate.Should().Be(30);
    }

    [Fact]
    public void Update_persists_and_notifies()
    {
        _store.Load().Returns(new AppSettings());
        var sut = new SettingsService(_store);
        SettingsChangedEventArgs? raised = null;
        sut.Changed += (_, e) => raised = e;

        sut.Update(s => s with { CaptureMicrophone = true });

        sut.Current.CaptureMicrophone.Should().BeTrue();
        _store.Received(1).Save(Arg.Is<AppSettings>(s => s.CaptureMicrophone));
        raised.Should().NotBeNull();
        raised!.OldSettings.CaptureMicrophone.Should().BeFalse();
        raised.NewSettings.CaptureMicrophone.Should().BeTrue();
    }

    [Fact]
    public void Update_without_change_is_a_no_op()
    {
        _store.Load().Returns(new AppSettings());
        var sut = new SettingsService(_store);
        var raised = false;
        sut.Changed += (_, _) => raised = true;

        sut.Update(s => s with { });

        raised.Should().BeFalse();
        _store.DidNotReceive().Save(Arg.Any<AppSettings>());
    }
}
