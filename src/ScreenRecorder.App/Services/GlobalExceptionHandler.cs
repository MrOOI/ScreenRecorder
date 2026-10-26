using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using ScreenRecorder.App.Diagnostics;

namespace ScreenRecorder.App.Services;

/// <summary>
/// Last line of defence: logs anything unhandled (which also writes an error report)
/// and keeps the app alive where possible.
/// </summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, ErrorReportWriter reports)
{
    public void Attach(Application application)
    {
        application.DispatcherUnhandledException += OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        logger.LogError(e.Exception, "Unhandled UI exception");
        e.Handled = true;
        if (e.Dispatcher.HasShutdownStarted)
        {
            return;
        }

        var where = reports.LastReportPath is { } path ? $"A report was saved to:\n{path}" : "Details were written to the log file.";
        MessageBox.Show(
            $"Something went wrong, but Screen Recorder is still running.\n\n{where}",
            "Screen Recorder", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        logger.LogError(e.Exception, "Unobserved task exception");
        e.SetObserved();
    }

    // The process is about to terminate; the report is written synchronously by the log sink.
    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e) =>
        logger.LogCritical(
            e.ExceptionObject as Exception ?? new InvalidOperationException($"Non-exception object thrown: {e.ExceptionObject}"),
            "Fatal unhandled exception (terminating: {IsTerminating})",
            e.IsTerminating);
}
