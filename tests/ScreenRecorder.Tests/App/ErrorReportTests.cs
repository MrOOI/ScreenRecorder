using ScreenRecorder.App.Diagnostics;
using ScreenRecorder.Infrastructure.Recording;
using Serilog.Events;
using Serilog.Parsing;

namespace ScreenRecorder.Tests.App;

public sealed class ErrorReportTests : IDisposable
{
    private static readonly DateTimeOffset At = new(2026, 10, 26, 14, 3, 12, 345, TimeSpan.FromHours(5));

    private readonly string _root = Path.Combine(Path.GetTempPath(), "sr-reports-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void Root_cause_is_the_innermost_exception()
    {
        var root = new FileNotFoundException("missing.dll");
        var outer = new InvalidOperationException("outer", new TypeInitializationException("Some.Type", root));

        ErrorReportFormatter.FindRootCause(outer).Should().BeSameAs(root);
        ErrorReportFormatter.FindRootCause(new AggregateException(root)).Should().BeSameAs(root);
    }

    [Fact]
    public void Report_starts_with_the_main_stack_of_the_root_cause_and_lists_the_chain()
    {
        var exception = Capture(() => Wrap(() => throw new ArgumentException("bad region")));

        var text = ErrorReportFormatter.Format(
            new ErrorReport(At, "Error", "Could not start recording", exception, "Engine",
                new Dictionary<string, string> { ["Options"] = "Region 640x360" }),
            applicationLogFolder: @"C:\logs");

        text.Should().Contain("Problem:         System.ArgumentException: bad region");
        var mainStack = text.IndexOf("MAIN STACK", StringComparison.Ordinal);
        var chain = text.IndexOf("EXCEPTION CHAIN", StringComparison.Ordinal);
        mainStack.Should().BeGreaterThan(0).And.BeLessThan(chain);
        text[mainStack..chain].Should().Contain(nameof(Report_starts_with_the_main_stack_of_the_root_cause_and_lists_the_chain));
        text.Should().Contain("[0] System.InvalidOperationException")
            .And.Contain("[1] System.ArgumentException")
            .And.Contain("Options:         Region 640x360")
            .And.Contain("Time (local):    2026-10-26 14:03:12.345 +05:00")
            .And.Contain("Application log: C:\\logs")
            .And.Contain("Runtime:");
    }

    [Fact]
    public void Native_failures_without_a_stack_are_explained()
    {
        var text = ErrorReportFormatter.Format(
            new ErrorReport(At, "Error", "Recording failed", new RecordingEngineException("E_FAIL", @"C:\a.mp4")));

        text.Should().Contain("reported by native code")
            .And.Contain("Data:        NativeError = E_FAIL")
            .And.Contain("Data:        OutputFile = C:\\a.mp4");
    }

    [Fact]
    public void File_goes_to_install_logs_date_folder_named_by_time_and_problem()
    {
        var sut = new ErrorReportWriter([_root]);

        var path = sut.Write(new ErrorReport(At, "Fatal", "Startup failed", new BadImageFormatException("bad")));

        path.Should().Be(Path.Combine(_root, "logs", "2026-10-26", "2026-10-26_14-03-12-345_BadImageFormatException.txt"));
        File.ReadAllText(path!).Should().Contain("BadImageFormatException: bad");
        sut.LastReportPath.Should().Be(path);
    }

    [Fact]
    public void Same_millisecond_reports_do_not_overwrite_each_other()
    {
        var sut = new ErrorReportWriter([_root]);
        var report = new ErrorReport(At, "Error", "x", new InvalidOperationException());

        var first = sut.Write(report);
        var second = sut.Write(report);

        second.Should().NotBe(first).And.EndWith("(2).txt");
    }

    [Fact]
    public void Unwritable_install_folder_falls_back_to_next_root()
    {
        Directory.CreateDirectory(_root);
        var blocked = Path.Combine(_root, "blocked");
        File.WriteAllText(blocked, "a file where the install folder should be");
        var fallback = Path.Combine(_root, "fallback");

        var path = new ErrorReportWriter([blocked, fallback])
            .Write(new ErrorReport(At, "Error", "x", new InvalidOperationException()));

        path.Should().StartWith(fallback);
    }

    [Fact]
    public void Writer_never_throws_when_nothing_is_writable()
    {
        Directory.CreateDirectory(_root);
        var blocked = Path.Combine(_root, "blocked");
        File.WriteAllText(blocked, "");

        new ErrorReportWriter([blocked])
            .Write(new ErrorReport(At, "Error", "x", new InvalidOperationException()))
            .Should().BeNull();
    }

    [Theory]
    [InlineData(LogEventLevel.Error, true, true)]
    [InlineData(LogEventLevel.Fatal, true, true)]
    [InlineData(LogEventLevel.Warning, true, false)]
    [InlineData(LogEventLevel.Error, false, false)]
    public void Sink_reports_only_errors_with_exceptions(LogEventLevel level, bool withException, bool expectReport)
    {
        var sut = new ErrorReportSink(new ErrorReportWriter([_root]));
        var template = new MessageTemplateParser().Parse("Could not save {Path}");
        var logEvent = new LogEvent(At, level, withException ? new IOException("disk full") : null, template,
        [
            new LogEventProperty("Path", new ScalarValue(@"C:\s.json")),
            new LogEventProperty("SourceContext", new ScalarValue("JsonSettingsStore")),
        ]);

        sut.Emit(logEvent);

        var files = Directory.Exists(_root) ? Directory.GetFiles(_root, "*.txt", SearchOption.AllDirectories) : [];
        files.Should().HaveCount(expectReport ? 1 : 0);
        if (expectReport)
        {
            File.ReadAllText(files[0]).Should().Contain("Message:         Could not save C:\\s.json")
                .And.Contain("Source:          JsonSettingsStore")
                .And.Contain("Path:            C:\\s.json");
        }
    }

    private static void Wrap(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Recording could not start", ex);
        }
    }

    private static Exception Capture(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            return ex;
        }

        throw new InvalidOperationException("Expected an exception.");
    }
}
