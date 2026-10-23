using Microsoft.Extensions.Time.Testing;
using ScreenRecorder.Core.Abstractions;
using ScreenRecorder.Core.Services;

namespace ScreenRecorder.Tests.Core;

public sealed class OutputPathResolverTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "sr-tests-" + Guid.NewGuid().ToString("N"));
    private readonly OutputPathResolver _sut;

    public OutputPathResolverTests()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 10, 23, 17, 3, 12, TimeSpan.Zero));
        time.SetLocalTimeZone(TimeZoneInfo.Utc);
        _sut = new OutputPathResolver(time);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void Creates_missing_folder_and_uses_timestamped_name()
    {
        var folder = Path.Combine(_root, "Videos");

        var path = _sut.ResolveNewFilePath(folder);

        Directory.Exists(folder).Should().BeTrue();
        path.Should().Be(Path.Combine(folder, "Recording 2026-10-23 17-03-12.mp4"));
        Directory.GetFiles(folder).Should().BeEmpty("the write probe must clean up after itself");
    }

    [Fact]
    public void Appends_counter_when_file_exists()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "Recording 2026-10-23 17-03-12.mp4"), "");
        File.WriteAllText(Path.Combine(_root, "Recording 2026-10-23 17-03-12 (2).mp4"), "");

        var path = _sut.ResolveNewFilePath(_root);

        Path.GetFileName(path).Should().Be("Recording 2026-10-23 17-03-12 (3).mp4");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_empty_folder(string folder)
    {
        _sut.Invoking(s => s.ResolveNewFilePath(folder))
            .Should().Throw<OutputFolderUnavailableException>()
            .WithMessage("*Settings*");
    }

    [Fact]
    public void Reports_unusable_folder_with_friendly_message()
    {
        Directory.CreateDirectory(_root);
        var file = Path.Combine(_root, "not-a-folder");
        File.WriteAllText(file, "");

        _sut.Invoking(s => s.ResolveNewFilePath(Path.Combine(file, "sub")))
            .Should().Throw<OutputFolderUnavailableException>()
            .WithMessage("Can't save to*");
    }
}
