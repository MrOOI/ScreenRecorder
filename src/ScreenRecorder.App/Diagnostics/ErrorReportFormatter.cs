using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace ScreenRecorder.App.Diagnostics;

/// <summary>
/// Renders an <see cref="ErrorReport"/> as plain text. The root cause and its stack come first,
/// followed by every exception in the chain, the full <see cref="Exception.ToString"/> and the environment.
/// </summary>
public static class ErrorReportFormatter
{
    private const string Rule = "------------------------------------------------------------";

    public static string Format(ErrorReport report, string? applicationLogFolder = null)
    {
        ArgumentNullException.ThrowIfNull(report);

        var rootCause = FindRootCause(report.Exception);
        var text = new StringBuilder(4096);

        text.AppendLine("SCREEN RECORDER — ERROR REPORT");
        text.AppendLine("============================================================");
        AppendField(text, "Time (local)", report.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture));
        AppendField(text, "Time (UTC)", report.Timestamp.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture));
        AppendField(text, "Level", report.Level);
        AppendField(text, "Source", report.Source ?? "(unknown)");
        AppendField(text, "Message", report.Message);
        AppendField(text, "Problem", $"{rootCause.GetType().FullName}: {rootCause.Message}");

        AppendSection(text, "MAIN STACK (where the problem started)");
        AppendMainStack(text, report.Exception, rootCause);

        AppendSection(text, "EXCEPTION CHAIN (outermost first)");
        var index = 0;
        foreach (var (exception, depth) in Walk(report.Exception))
        {
            AppendException(text, exception, index++, depth);
        }

        if (report.Properties is { Count: > 0 } properties)
        {
            AppendSection(text, "CONTEXT");
            foreach (var (key, value) in properties.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                AppendField(text, key, value);
            }
        }

        AppendSection(text, "FULL EXCEPTION");
        text.AppendLine(report.Exception.ToString());

        AppendSection(text, "ENVIRONMENT");
        AppendEnvironment(text, applicationLogFolder);

        return text.ToString();
    }

    /// <summary>
    /// The innermost exception: the one that actually started the failure.
    /// For aggregates, the first inner exception is followed.
    /// </summary>
    public static Exception FindRootCause(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var current = exception;
        while (true)
        {
            var next = current is AggregateException { InnerExceptions.Count: > 0 } aggregate
                ? aggregate.InnerExceptions[0]
                : current.InnerException;
            if (next is null)
            {
                return current;
            }

            current = next;
        }
    }

    /// <summary>Depth-first walk over inner exceptions, including every branch of an aggregate.</summary>
    public static IEnumerable<(Exception Exception, int Depth)> Walk(Exception exception)
    {
        var pending = new Stack<(Exception, int)>();
        pending.Push((exception, 0));
        while (pending.Count > 0)
        {
            var (current, depth) = pending.Pop();
            yield return (current, depth);

            if (current is AggregateException aggregate)
            {
                for (var i = aggregate.InnerExceptions.Count - 1; i >= 0; i--)
                {
                    pending.Push((aggregate.InnerExceptions[i], depth + 1));
                }
            }
            else if (current.InnerException is { } inner)
            {
                pending.Push((inner, depth + 1));
            }
        }
    }

    private static void AppendMainStack(StringBuilder text, Exception outermost, Exception rootCause)
    {
        text.AppendLine($"{rootCause.GetType().FullName}: {rootCause.Message}");

        // An exception that was created but never thrown has no stack; use the nearest one that has.
        var withStack = rootCause.StackTrace is not null
            ? rootCause
            : Walk(outermost).Select(e => e.Exception).LastOrDefault(e => e.StackTrace is not null);

        if (withStack is null)
        {
            text.AppendLine("(No managed stack trace: the failure was reported by native code rather than thrown.)");
            return;
        }

        if (!ReferenceEquals(withStack, rootCause))
        {
            text.AppendLine($"(Stack of the nearest thrown exception: {withStack.GetType().FullName})");
        }

        text.AppendLine(withStack.StackTrace);
    }

    private static void AppendException(StringBuilder text, Exception exception, int index, int depth)
    {
        var indent = new string(' ', depth * 2);
        text.AppendLine($"{indent}[{index}] {exception.GetType().FullName}");
        text.AppendLine($"{indent}    Message:     {exception.Message}");
        text.AppendLine($"{indent}    HResult:     0x{exception.HResult:X8}");
        if (exception.Source is { } source)
        {
            text.AppendLine($"{indent}    Source:      {source}");
        }

        if (exception.TargetSite is { } site)
        {
            text.AppendLine($"{indent}    Target site: {site.DeclaringType?.FullName}.{site.Name}");
        }

        foreach (DictionaryEntry entry in exception.Data)
        {
            text.AppendLine($"{indent}    Data:        {entry.Key} = {entry.Value}");
        }

        text.AppendLine($"{indent}    Stack trace:");
        var stack = exception.StackTrace ?? "(none)";
        foreach (var line in stack.Split(Environment.NewLine))
        {
            text.AppendLine($"{indent}      {line.Trim()}");
        }

        text.AppendLine();
    }

    private static void AppendEnvironment(StringBuilder text, string? applicationLogFolder)
    {
        var entry = Assembly.GetEntryAssembly();
        using var process = Process.GetCurrentProcess();

        AppendField(text, "App version", entry?.GetName().Version?.ToString() ?? "(unknown)");
        AppendField(text, "Executable", Environment.ProcessPath ?? "(unknown)");
        AppendField(text, "Install folder", AppContext.BaseDirectory);
        AppendField(text, "Process", $"{Environment.ProcessId}, {(Environment.Is64BitProcess ? "64-bit" : "32-bit")}");
        AppendField(text, "Started", process.StartTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        AppendField(text, "Thread", $"{Environment.CurrentManagedThreadId} {Thread.CurrentThread.Name}".TrimEnd());
        AppendField(text, "OS", $"{RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})");
        AppendField(text, "Runtime", $"{RuntimeInformation.FrameworkDescription} ({RuntimeInformation.ProcessArchitecture})");
        AppendField(text, "Culture", $"{CultureInfo.CurrentCulture.Name} / UI {CultureInfo.CurrentUICulture.Name}");
        AppendField(text, "Memory", $"{process.WorkingSet64 / (1024 * 1024)} MB working set, {GC.GetTotalMemory(false) / (1024 * 1024)} MB managed");
        AppendField(text, "Processors", Environment.ProcessorCount.ToString(CultureInfo.InvariantCulture));
        if (applicationLogFolder is not null)
        {
            AppendField(text, "Application log", applicationLogFolder);
        }
    }

    private static void AppendSection(StringBuilder text, string title)
    {
        text.AppendLine();
        text.AppendLine(title);
        text.AppendLine(Rule);
    }

    private static void AppendField(StringBuilder text, string name, string value) =>
        text.AppendLine($"{name + ":",-16} {value}");
}
