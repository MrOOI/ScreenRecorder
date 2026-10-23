using Microsoft.Extensions.Logging.Abstractions;
using ScreenRecorder.Core.Models;
using ScreenRecorder.Infrastructure.Settings;

namespace ScreenRecorder.Tests.Infrastructure;

public sealed class JsonSettingsStoreTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "sr-settings-" + Guid.NewGuid().ToString("N"));
    private readonly string _file;
    private readonly JsonSettingsStore _sut;

    public JsonSettingsStoreTests()
    {
        _file = Path.Combine(_folder, "settings.json");
        _sut = new JsonSettingsStore(_file, NullLogger<JsonSettingsStore>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public void Missing_file_yields_defaults()
    {
        _sut.Load().Should().Be(new AppSettings());
    }

    [Fact]
    public void Round_trips_all_values()
    {
        var settings = new AppSettings
        {
            OutputFolder = @"D:\Captures",
            CaptureSystemAudio = false,
            CaptureMicrophone = true,
            ShowCursor = false,
            FrameRate = 60,
            Quality = VideoQuality.High,
            CountdownEnabled = false,
            CountdownSeconds = 5,
            StartMinimized = true,
            StartStopHotkey = new Hotkey(HotkeyModifiers.Alt | HotkeyModifiers.Shift, 0x78),
        };

        _sut.Save(settings);

        _sut.Load().Should().Be(settings);
        File.Exists(_file + ".tmp").Should().BeFalse("the temp file is moved into place");
    }

    [Fact]
    public void Writes_readable_json_with_enum_names()
    {
        _sut.Save(new AppSettings { Quality = VideoQuality.High });

        var json = File.ReadAllText(_file);
        json.Should().Contain("\"quality\": \"High\"").And.NotContain("effectiveCountdown");
    }

    [Fact]
    public void Corrupt_file_yields_defaults_and_is_preserved()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(_file, "{ not json");

        _sut.Load().Should().Be(new AppSettings());
        File.Exists(_file + ".corrupt").Should().BeTrue();
    }

    [Fact]
    public void Partial_file_keeps_defaults_for_missing_values()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(_file, """{ "frameRate": 60 }""");

        var loaded = _sut.Load();

        loaded.FrameRate.Should().Be(60);
        loaded.ShowCursor.Should().BeTrue();
        loaded.StartStopHotkey.Should().Be(new AppSettings().StartStopHotkey);
    }
}
