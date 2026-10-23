using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;

namespace ScreenRecorder.App.Services;

/// <summary>
/// Last line of defence: logs anything unhandled and keeps the app alive where possible.
/// </summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
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

        MessageBox.Show(
            "Something went wrong, but Screen Recorder is still running.\n\nDetails were written to the log file.",
            "Screen Recorder", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        logger.LogError(e.Exception, "Unobserved task exception");
        e.SetObserved();
    }

    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e) =>
        logger.LogCritical(e.ExceptionObject as Exception, "Fatal unhandled exception");
}
