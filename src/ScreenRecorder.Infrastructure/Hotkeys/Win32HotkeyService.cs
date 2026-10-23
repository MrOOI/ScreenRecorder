using System.Windows.Interop;
using Microsoft.Extensions.Logging;
using ScreenRecorder.Core.Abstractions;
using ScreenRecorder.Core.Models;
using static ScreenRecorder.Infrastructure.Interop.NativeMethods;

namespace ScreenRecorder.Infrastructure.Hotkeys;

/// <summary>
/// Global hotkeys via <c>RegisterHotKey</c> on a hidden message-only window.
/// Must be created on the UI thread, whose dispatcher pumps the window's messages.
/// </summary>
public sealed class Win32HotkeyService : IHotkeyService
{
    private static readonly nint HwndMessage = -3;

    private readonly ILogger<Win32HotkeyService> _logger;
    private readonly HwndSource _window;
    private readonly HashSet<HotkeyAction> _registered = [];

    public Win32HotkeyService(ILogger<Win32HotkeyService> logger)
    {
        _logger = logger;
        _window = new HwndSource(new HwndSourceParameters("ScreenRecorder.Hotkeys")
        {
            ParentWindow = HwndMessage,
            WindowStyle = 0,
        });
        _window.AddHook(WndProc);
    }

    public event EventHandler<HotkeyPressedEventArgs>? Pressed;

    public bool TryRegister(HotkeyAction action, Hotkey hotkey)
    {
        ArgumentNullException.ThrowIfNull(hotkey);
        Unregister(action);

        if (!hotkey.IsValid)
        {
            return false;
        }

        var modifiers = (uint)hotkey.Modifiers | ModNoRepeat;
        if (!RegisterHotKey(_window.Handle, IdOf(action), modifiers, (uint)hotkey.VirtualKey))
        {
            _logger.LogWarning("Hotkey {Hotkey} for {Action} is in use by another application", hotkey, action);
            return false;
        }

        _registered.Add(action);
        _logger.LogInformation("Registered {Hotkey} for {Action}", hotkey, action);
        return true;
    }

    public void UnregisterAll()
    {
        foreach (var action in _registered.ToArray())
        {
            Unregister(action);
        }
    }

    public void Dispose()
    {
        UnregisterAll();
        _window.RemoveHook(WndProc);
        _window.Dispose();
    }

    // Ids start at 1; zero is reserved by some shells.
    private static int IdOf(HotkeyAction action) => (int)action + 1;

    private void Unregister(HotkeyAction action)
    {
        if (_registered.Remove(action))
        {
            UnregisterHotKey(_window.Handle, IdOf(action));
        }
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg != WmHotkey)
        {
            return 0;
        }

        var action = (HotkeyAction)((int)wParam - 1);
        if (_registered.Contains(action))
        {
            handled = true;
            Pressed?.Invoke(this, new HotkeyPressedEventArgs(action));
        }

        return 0;
    }
}
