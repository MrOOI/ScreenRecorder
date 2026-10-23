using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace ScreenRecorder.App.Services;

public interface IFolderPicker
{
    /// <summary>Returns the chosen folder, or null when cancelled.</summary>
    string? PickFolder(string initialFolder);
}

public sealed class FolderPicker : IFolderPicker
{
    public string? PickFolder(string initialFolder)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Choose where recordings are saved",
            InitialDirectory = Directory.Exists(initialFolder) ? initialFolder : string.Empty,
        };

        var owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
        return dialog.ShowDialog(owner) == true ? dialog.FolderName : null;
    }
}
