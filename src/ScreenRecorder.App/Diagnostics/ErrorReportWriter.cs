using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using ScreenRecorder.Infrastructure;

namespace ScreenRecorder.App.Diagnostics;

/// <summary>
/// Writes one text file per failure:
/// <c>&lt;install folder&gt;\logs\&lt;yyyy-MM-dd&gt;\&lt;yyyy-MM-dd_HH-mm-ss-fff&gt;_&lt;Problem&gt;.txt</c>.
/// If the install folder is not writable (for example under Program Files), the next root is used.
/// Never throws: reporting a failure must not cause another one.
/// </summary>
public sealed class ErrorReportWriter
{
    public const string LogsFolderName = "logs";

    private readonly IReadOnlyList<string> _roots;
    private readonly string? _applicationLogFolder;
    private readonly object _gate = new();

    /// <param name="roots">Candidate base folders, tried in order.</param>
    /// <param name="applicationLogFolder">Where the rolling application log lives; named in each report.</param>
    public ErrorReportWriter(IEnumerable<string> roots, string? applicationLogFolder = null)
    {
        _roots = roots?.ToArray() ?? throw new ArgumentNullException(nameof(roots));
        if (_roots.Count == 0)
        {
            throw new ArgumentException("At least one root folder is required.", nameof(roots));
        }

        _applicationLogFolder = applicationLogFolder;
    }

    /// <summary>The path of the most recent report, if any was written.</summary>
    public string? LastReportPath { get; private set; }

    /// <summary>Install folder first, then a per-user fallback.</summary>
    public static ErrorReportWriter CreateDefault() =>
        new([AppContext.BaseDirectory, AppDataPaths.LocalRoot], AppDataPaths.LogsFolder);

    /// <summary>Writes the report and returns its path, or null if no folder was writable.</summary>
    public string? Write(ErrorReport report)
    {
        string text;
        try
        {
            text = ErrorReportFormatter.Format(report, _applicationLogFolder);
        }
        catch (Exception ex)
        {
            // Formatting must never hide the original problem.
            text = $"Error report formatting failed ({ex.Message}).{Environment.NewLine}{report.Exception}";
        }

        lock (_gate)
        {
            foreach (var root in _roots)
            {
                try
                {
                    var folder = Path.Combine(root, LogsFolderName,
                        report.Timestamp.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                    Directory.CreateDirectory(folder);
                    var path = UniquePath(folder, FileNameFor(report));
                    File.WriteAllText(path, text, Encoding.UTF8);
                    LastReportPath = path;
                    return path;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException
                                               or ArgumentException)
                {
                    Debug.WriteLine($"Error report not written to {root}: {ex.Message}");
                }
            }
        }

        return null;
    }

    /// <summary>
    /// <c>2026-10-26_14-03-12-345_BadImageFormatException</c>: when, then what started the problem.
    /// </summary>
    public static string FileNameFor(ErrorReport report)
    {
        var stamp = report.Timestamp.ToString("yyyy-MM-dd_HH-mm-ss-fff", CultureInfo.InvariantCulture);
        var problem = ErrorReportFormatter.FindRootCause(report.Exception).GetType().Name;
        var safe = string.Concat(problem.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        return $"{stamp}_{safe}";
    }

    private static string UniquePath(string folder, string baseName)
    {
        var path = Path.Combine(folder, baseName + ".txt");
        for (var n = 2; File.Exists(path); n++)
        {
            path = Path.Combine(folder, $"{baseName} ({n}).txt");
        }

        return path;
    }
}
