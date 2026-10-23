using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using Microsoft.Extensions.Logging;
using ScreenRecorder.Core.Abstractions;

namespace ScreenRecorder.Infrastructure.Shell;

/// <summary>
/// Opens files and folders through Windows Explorer and file associations.
/// </summary>
public sealed class WindowsShellService(ILogger<WindowsShellService> logger) : IShellService
{
    public void OpenFile(string filePath) =>
        Launch(new ProcessStartInfo(filePath) { UseShellExecute = true });

    public void RevealFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            OpenFolder(Path.GetDirectoryName(filePath) ?? filePath);
            return;
        }

        // Explorer parses its own command line; "/select," must be glued to the quoted path.
        Launch(new ProcessStartInfo("explorer.exe", $"/select,\"{filePath}\""));
    }

    public void OpenFolder(string folderPath)
    {
        try
        {
            Directory.CreateDirectory(folderPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            logger.LogWarning(ex, "Could not create folder {Folder}", folderPath);
        }

        Launch(new ProcessStartInfo(folderPath) { UseShellExecute = true });
    }

    private void Launch(ProcessStartInfo startInfo)
    {
        try
        {
            using var _ = Process.Start(startInfo);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or FileNotFoundException)
        {
            logger.LogWarning(ex, "Could not open {Target}", startInfo.FileName);
        }
    }
}
