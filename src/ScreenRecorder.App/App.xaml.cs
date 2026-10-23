using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ScreenRecorder.App.Composition;
using ScreenRecorder.App.Services;
using ScreenRecorder.App.ViewModels;
using ScreenRecorder.App.Views;
using ScreenRecorder.Core.Abstractions;
using ScreenRecorder.Infrastructure;
using Serilog;

namespace ScreenRecorder.App;

public partial class App : Application
{
    private readonly SingleInstanceGuard _instanceGuard = new();
    private IHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (!_instanceGuard.IsFirstInstance)
        {
            _instanceGuard.SignalFirstInstance();
            Shutdown();
            return;
        }

        _host = BuildHost();
        _host.Start();
        var services = _host.Services;

        services.GetRequiredService<GlobalExceptionHandler>().Attach(this);
        services.GetRequiredService<ThemeService>().Apply(this);

        var window = services.GetRequiredService<MainWindow>();
        MainWindow = window;

        services.GetRequiredService<RecordingUiCoordinator>().Start();
        services.GetRequiredService<TrayIconService>().Show();

        var hotkeys = services.GetRequiredService<HotkeyManager>();
        var main = services.GetRequiredService<MainViewModel>();
        hotkeys.Triggered += (_, args) => main.HandleHotkey(args.Action);
        hotkeys.Start();

        var windowService = services.GetRequiredService<IAppWindowService>();
        _instanceGuard.ListenForActivation(() => Dispatcher.BeginInvoke(windowService.Restore));

        if (!services.GetRequiredService<ISettingsService>().Current.StartMinimized)
        {
            window.Show();
        }

        services.GetRequiredService<ILogger<App>>().LogInformation("Screen Recorder started");
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            var logger = _host.Services.GetRequiredService<ILogger<App>>();
            logger.LogInformation("Screen Recorder exiting");
            try
            {
                _host.StopAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();

                // Disposes singletons: engine, hotkeys, tray icon, timers.
                _host.Dispose();
            }
            catch (Exception ex)
            {
                // Never block exit on cleanup: a dialog here would keep the process alive.
                logger.LogError(ex, "Error while shutting down");
            }
        }

        _instanceGuard.Dispose();
        base.OnExit(e);
    }

    private IHost BuildHost()
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            ContentRootPath = AppContext.BaseDirectory,
            DisableDefaults = true,
        });

        var logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.File(
                Path.Combine(AppDataPaths.LogsFolder, "log-.txt"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        builder.Logging.ClearProviders();
        builder.Logging.AddSerilog(logger, dispose: true);
        builder.Services.AddScreenRecorder(this);

        return builder.Build();
    }
}
