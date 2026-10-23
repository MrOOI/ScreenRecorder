namespace ScreenRecorder.Core.Models;

/// <summary>
/// A physical display attached to the system.
/// </summary>
/// <param name="DeviceName">The GDI device name, for example <c>\\.\DISPLAY1</c>.</param>
/// <param name="Index">One-based position used for user-facing names.</param>
/// <param name="Bounds">Bounds in virtual-screen physical pixels.</param>
/// <param name="IsPrimary">Whether this is the primary display.</param>
public sealed record DisplayInfo(string DeviceName, int Index, ScreenRegion Bounds, bool IsPrimary);
