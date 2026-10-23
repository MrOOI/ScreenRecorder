using ScreenRecorder.Core.Models;

namespace ScreenRecorder.Core.Abstractions;

/// <summary>
/// Persists <see cref="AppSettings"/>.
/// </summary>
public interface ISettingsStore
{
    /// <summary>Returns stored settings, or defaults when nothing usable is stored. Never throws.</summary>
    AppSettings Load();

    void Save(AppSettings settings);
}
