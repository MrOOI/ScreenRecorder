namespace ScreenRecorder.App.Diagnostics;

/// <summary>
/// Everything known about one failure, ready to be written as a report file.
/// </summary>
/// <param name="Timestamp">When the failure happened (local offset preserved).</param>
/// <param name="Level">Severity, e.g. <c>Error</c> or <c>Fatal</c>.</param>
/// <param name="Message">What the application was doing, in words.</param>
/// <param name="Exception">The failure itself.</param>
/// <param name="Source">The component that reported it, usually a class name.</param>
/// <param name="Properties">Structured context such as file paths or recording options.</param>
public sealed record ErrorReport(
    DateTimeOffset Timestamp,
    string Level,
    string Message,
    Exception Exception,
    string? Source = null,
    IReadOnlyDictionary<string, string>? Properties = null);
