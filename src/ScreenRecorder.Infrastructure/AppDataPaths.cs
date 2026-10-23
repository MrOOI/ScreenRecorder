using System.IO;

namespace ScreenRecorder.Infrastructure;

/// <summary>
/// Per-user locations for application data.
/// </summary>
public static class AppDataPaths
{
    public static string Root { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ScreenRecorder");

    public static string SettingsFile { get; } = Path.Combine(Root, "settings.json");

    public static string LogsFolder { get; } = Path.Combine(Root, "logs");
}
