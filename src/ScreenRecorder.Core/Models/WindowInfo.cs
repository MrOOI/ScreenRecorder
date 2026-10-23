namespace ScreenRecorder.Core.Models;

/// <summary>
/// A top-level application window that can be recorded.
/// </summary>
public sealed record WindowInfo(nint Handle, string Title, string ProcessName);
