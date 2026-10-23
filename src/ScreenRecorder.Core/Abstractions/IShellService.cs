namespace ScreenRecorder.Core.Abstractions;

/// <summary>
/// Hands files and folders to the operating system shell.
/// </summary>
public interface IShellService
{
    /// <summary>Opens the file with its default application.</summary>
    void OpenFile(string filePath);

    /// <summary>Opens the containing folder with the file selected.</summary>
    void RevealFile(string filePath);

    void OpenFolder(string folderPath);
}
