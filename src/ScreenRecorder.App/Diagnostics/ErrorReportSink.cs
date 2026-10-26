using System.Globalization;
using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;

namespace ScreenRecorder.App.Diagnostics;

/// <summary>
/// Serilog sink that turns every Error or Fatal event carrying an exception into a report file.
/// Because all failure paths already log, no call site needs to know about reports.
/// </summary>
public sealed class ErrorReportSink(ErrorReportWriter writer) : ILogEventSink
{
    private const string SourceContextProperty = "SourceContext";

    public void Emit(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);

        if (logEvent.Exception is null || logEvent.Level < LogEventLevel.Error)
        {
            return;
        }

        var properties = logEvent.Properties
            .Where(p => p.Key != SourceContextProperty)
            .ToDictionary(p => p.Key, p => Render(p.Value));

        writer.Write(new ErrorReport(
            logEvent.Timestamp,
            logEvent.Level.ToString(),
            RenderMessage(logEvent),
            logEvent.Exception,
            logEvent.Properties.TryGetValue(SourceContextProperty, out var source) ? Render(source) : null,
            properties));
    }

    // Like Serilog's ":l" format: string values appear without quotes, as people would write them.
    private static string RenderMessage(LogEvent logEvent) => string.Concat(logEvent.MessageTemplate.Tokens.Select(token =>
        token is PropertyToken property && logEvent.Properties.TryGetValue(property.PropertyName, out var value)
            ? Render(value)
            : token.ToString()));

    private static string Render(LogEventPropertyValue value) =>
        value is ScalarValue { Value: string text } ? text : value.ToString(null, CultureInfo.InvariantCulture);
}
