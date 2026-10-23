using ScreenRecorder.Core.Models;

namespace ScreenRecorder.Tests.Core;

public sealed class ModelTests
{
    [Fact]
    public void ScreenRegion_from_points_normalizes_corners()
    {
        ScreenRegion.FromPoints(100, 80, 20, 10).Should().Be(new ScreenRegion(20, 10, 80, 70));
    }

    [Fact]
    public void ScreenRegion_intersect_clips_to_bounds()
    {
        var display = new ScreenRegion(0, 0, 100, 100);

        new ScreenRegion(80, 90, 50, 50).Intersect(display).Should().Be(new ScreenRegion(80, 90, 20, 10));
        new ScreenRegion(200, 200, 10, 10).Intersect(display).IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void ScreenRegion_with_even_size_trims_odd_dimensions()
    {
        new ScreenRegion(1, 1, 641, 481).WithEvenSize().Should().Be(new ScreenRegion(1, 1, 640, 480));
    }

    [Theory]
    [InlineData(HotkeyModifiers.Control | HotkeyModifiers.Shift, 'R', "Ctrl+Shift+R")]
    [InlineData(HotkeyModifiers.Alt, 0x71, "Alt+F2")]
    [InlineData(HotkeyModifiers.Windows | HotkeyModifiers.Control, '5', "Ctrl+Win+5")]
    public void Hotkey_formats_for_display(HotkeyModifiers modifiers, int key, string expected)
    {
        new Hotkey(modifiers, key).ToString().Should().Be(expected);
    }

    [Fact]
    public void Hotkey_without_modifier_is_invalid()
    {
        new Hotkey(HotkeyModifiers.None, 'R').IsValid.Should().BeFalse();
    }

    [Fact]
    public void AppSettings_normalize_repairs_invalid_values()
    {
        var broken = new AppSettings
        {
            SchemaVersion = 0,
            OutputFolder = " ",
            FrameRate = 7,
            Quality = (VideoQuality)42,
            CountdownSeconds = 99,
            StartStopHotkey = new Hotkey(HotkeyModifiers.None, 0),
        };

        var fixedSettings = broken.Normalize();

        fixedSettings.SchemaVersion.Should().Be(AppSettings.CurrentSchemaVersion);
        fixedSettings.OutputFolder.Should().Be(AppSettings.DefaultOutputFolder);
        fixedSettings.FrameRate.Should().Be(30);
        fixedSettings.Quality.Should().Be(VideoQuality.Medium);
        fixedSettings.CountdownSeconds.Should().Be(10);
        fixedSettings.StartStopHotkey.Should().Be(new AppSettings().StartStopHotkey);
    }

    [Fact]
    public void AppSettings_effective_countdown_honours_toggle()
    {
        new AppSettings { CountdownEnabled = false }.EffectiveCountdown.Should().Be(TimeSpan.Zero);
        new AppSettings { CountdownSeconds = 3 }.EffectiveCountdown.Should().Be(TimeSpan.FromSeconds(3));
    }

    [Theory]
    [InlineData(RecordingState.Idle, false, true)]
    [InlineData(RecordingState.Failed, false, true)]
    [InlineData(RecordingState.Countdown, true, false)]
    [InlineData(RecordingState.Recording, true, false)]
    [InlineData(RecordingState.Paused, true, false)]
    [InlineData(RecordingState.Finishing, true, false)]
    public void RecordingState_classification(RecordingState state, bool active, bool canStart)
    {
        state.IsActive().Should().Be(active);
        state.CanStart().Should().Be(canStart);
    }
}
