using ScreenRecorder.Core.Models;

namespace ScreenRecorder.Core.Abstractions;

/// <summary>
/// The application-wide current settings, with change notification and persistence.
/// </summary>
public interface ISettingsService
{
    AppSettings Current { get; }

    event EventHandler<SettingsChangedEventArgs>? Changed;

    /// <summary>Applies a change, persists it and notifies subscribers. No-op when nothing changed.</summary>
    void Update(Func<AppSettings, AppSettings> change);
}

public sealed class SettingsChangedEventArgs(AppSettings oldSettings, AppSettings newSettings) : EventArgs
{
    public AppSettings OldSettings { get; } = oldSettings;

    public AppSettings NewSettings { get; } = newSettings;
}
