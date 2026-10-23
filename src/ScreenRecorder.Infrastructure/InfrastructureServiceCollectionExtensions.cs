using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ScreenRecorder.Core.Abstractions;
using ScreenRecorder.Infrastructure.Capture;
using ScreenRecorder.Infrastructure.Hotkeys;
using ScreenRecorder.Infrastructure.Recording;
using ScreenRecorder.Infrastructure.Settings;
using ScreenRecorder.Infrastructure.Shell;

namespace ScreenRecorder.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Windows implementations of the Core abstractions.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string settingsFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsFilePath);

        services.AddSingleton<ICaptureSourceProvider, Win32CaptureSourceProvider>();
        services.AddSingleton<IAudioSourceFactory, DefaultAudioSourceFactory>();
        services.AddSingleton<RecordingOptionsMapper>();
        services.AddSingleton<IRecordingEngine, ScreenRecorderLibEngine>();
        services.AddSingleton<IHotkeyService, Win32HotkeyService>();
        services.AddSingleton<IShellService, WindowsShellService>();
        services.AddSingleton<ISettingsStore>(sp =>
            new JsonSettingsStore(settingsFilePath, sp.GetRequiredService<ILogger<JsonSettingsStore>>()));

        return services;
    }
}
