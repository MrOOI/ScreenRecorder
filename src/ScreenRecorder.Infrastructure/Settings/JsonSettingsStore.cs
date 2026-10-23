using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using ScreenRecorder.Core.Abstractions;
using ScreenRecorder.Core.Models;

namespace ScreenRecorder.Infrastructure.Settings;

/// <summary>
/// Stores settings as indented JSON. Writes are atomic; unreadable files fall back to defaults.
/// </summary>
public sealed class JsonSettingsStore(string filePath, ILogger<JsonSettingsStore> logger) : ISettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        IgnoreReadOnlyProperties = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public AppSettings Load()
    {
        if (!File.Exists(filePath))
        {
            return new AppSettings();
        }

        try
        {
            using var stream = File.OpenRead(filePath);
            var settings = JsonSerializer.Deserialize<AppSettings>(stream, JsonOptions);
            return (settings ?? new AppSettings()).Normalize();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            logger.LogWarning(ex, "Settings file {Path} is unreadable; using defaults", filePath);
            PreserveCorruptFile();
            return new AppSettings();
        }
    }

    /// <summary>Persists settings. Failures are logged, not thrown: losing a preference must not stop the app.</summary>
    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var tempPath = filePath + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            File.WriteAllText(tempPath, JsonSerializer.Serialize(settings, JsonOptions));
            File.Move(tempPath, filePath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogError(ex, "Could not save settings to {Path}", filePath);
        }
    }

    private void PreserveCorruptFile()
    {
        try
        {
            File.Move(filePath, filePath + ".corrupt", overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogDebug(ex, "Could not move corrupt settings file aside");
        }
    }
}
