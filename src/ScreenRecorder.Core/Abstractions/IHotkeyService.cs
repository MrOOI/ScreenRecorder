using ScreenRecorder.Core.Models;

namespace ScreenRecorder.Core.Abstractions;

/// <summary>
/// System-wide hotkeys that work while other applications have focus.
/// </summary>
public interface IHotkeyService : IDisposable
{
    event EventHandler<HotkeyPressedEventArgs>? Pressed;

    /// <summary>
    /// Binds <paramref name="hotkey"/> to <paramref name="action"/>, replacing any previous binding for that action.
    /// </summary>
    /// <returns>False when the combination is invalid or already taken by another application.</returns>
    bool TryRegister(HotkeyAction action, Hotkey hotkey);

    void UnregisterAll();
}

public sealed class HotkeyPressedEventArgs(HotkeyAction action) : EventArgs
{
    public HotkeyAction Action { get; } = action;
}
