using System.IO;
using ScreenRecorder.App.ViewModels;
using ScreenRecorder.Core.Abstractions;
using ScreenRecorder.Core.Models;

namespace ScreenRecorder.App.Services;

/// <summary>
/// Presents notifications as the in-window banner. Safe to call from any thread.
/// </summary>
public sealed class BannerNotificationService(BannerViewModel banner, IUiDispatcher dispatcher) : INotificationService
{
    public void ShowRecordingResult(RecordingResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result is { IsSuccess: true, FilePath: { } path })
        {
            var message = $"Saved {Format(result.Duration)} · {Path.GetFileName(path)}";
            dispatcher.Post(() => banner.ShowSuccess(message, path));
        }
        else
        {
            ShowError(result.ErrorMessage ?? "The recording failed.");
        }
    }

    public void ShowError(string message) => dispatcher.Post(() => banner.ShowError(message));

    private static string Format(TimeSpan duration) =>
        duration.TotalHours >= 1 ? duration.ToString(@"h\:mm\:ss") : duration.ToString(@"m\:ss");
}
