using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using ScreenRecorder.App.Services;
using ScreenRecorder.App.ViewModels;
using ScreenRecorder.App.Views;
using ScreenRecorder.Core.Abstractions;
using ScreenRecorder.Core.Services;
using ScreenRecorder.Infrastructure;

namespace ScreenRecorder.App.Composition;

/// <summary>
/// The composition root: the only place that knows concrete types across all layers.
/// </summary>
internal static class ServiceRegistration
{
    public static IServiceCollection AddScreenRecorder(this IServiceCollection services, Application application)
    {
        services.AddSingleton(TimeProvider.System);

        // Core
        services.AddSingleton<RecordingSession>();
        services.AddSingleton<IRecordingSession>(sp => sp.GetRequiredService<RecordingSession>());
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IOutputPathResolver, OutputPathResolver>();
        services.AddSingleton<IRecordingLauncher, RecordingLauncher>();

        // Infrastructure
        services.AddInfrastructure(AppDataPaths.SettingsFile);

        // Presentation services
        services.AddSingleton<IUiDispatcher>(new WpfUiDispatcher(application.Dispatcher));
        services.AddSingleton<IAppLifetime, WpfAppLifetime>();
        services.AddSingleton<IAppWindowService, MainWindowService>();
        services.AddSingleton<INotificationService, BannerNotificationService>();
        services.AddSingleton<ICountdownPresenter, CountdownPresenter>();
        services.AddSingleton<IRegionPicker, RegionPicker>();
        services.AddSingleton<IFolderPicker, FolderPicker>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<HotkeyManager>();
        services.AddSingleton<RecordingUiCoordinator>();
        services.AddSingleton<TrayIconService>();
        services.AddSingleton<ThemeService>();
        services.AddSingleton<GlobalExceptionHandler>();

        // View models and views
        services.AddSingleton<BannerViewModel>();
        services.AddSingleton<SourceSelectorViewModel>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<TrayViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<WindowPickerViewModel>();
        services.AddSingleton<Func<SettingsViewModel>>(sp => sp.GetRequiredService<SettingsViewModel>);
        services.AddSingleton<Func<WindowPickerViewModel>>(sp => sp.GetRequiredService<WindowPickerViewModel>);
        services.AddSingleton<MainWindow>();

        return services;
    }
}
