using System.Globalization;
using ScreenRecorder.Core.Abstractions;

namespace ScreenRecorder.Core.Services;

/// <summary>
/// Produces names like <c>Recording 2026-10-23 17-03-12.mp4</c>, adding <c> (2)</c> on collision.
/// </summary>
public sealed class OutputPathResolver(TimeProvider time) : IOutputPathResolver
{
    public const string FileExtension = ".mp4";

    private readonly TimeProvider _time = time ?? throw new ArgumentNullException(nameof(time));

    public string ResolveNewFilePath(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
        {
            throw new OutputFolderUnavailableException("No output folder is set. Choose one in Settings.");
        }

        EnsureWritable(folder);

        var stamp = _time.GetLocalNow().ToString("yyyy-MM-dd HH-mm-ss", CultureInfo.InvariantCulture);
        var baseName = $"Recording {stamp}";
        var path = Path.Combine(folder, baseName + FileExtension);
        for (var n = 2; File.Exists(path); n++)
        {
            path = Path.Combine(folder, $"{baseName} ({n}){FileExtension}");
        }

        return path;
    }

    private static void EnsureWritable(string folder)
    {
        try
        {
            Directory.CreateDirectory(folder);
            var probe = Path.Combine(folder, $".write-test-{Guid.NewGuid():N}");
            using (File.Create(probe, 1, FileOptions.DeleteOnClose))
            {
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            throw new OutputFolderUnavailableException(
                $"Can't save to \"{folder}\". Check that the drive is connected and writable, or choose another folder in Settings.",
                ex);
        }
    }
}
