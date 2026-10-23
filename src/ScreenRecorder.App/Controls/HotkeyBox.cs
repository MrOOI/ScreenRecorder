using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ScreenRecorder.Core.Models;

namespace ScreenRecorder.App.Controls;

/// <summary>
/// A read-only text box that captures a key combination when focused.
/// Only combinations with at least one modifier are accepted.
/// </summary>
public sealed class HotkeyBox : TextBox
{
    public static readonly DependencyProperty HotkeyProperty = DependencyProperty.Register(
        nameof(Hotkey),
        typeof(Hotkey),
        typeof(HotkeyBox),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnHotkeyChanged));

    public HotkeyBox()
    {
        IsReadOnly = true;
        IsReadOnlyCaretVisible = false;
        IsUndoEnabled = false;
        Cursor = Cursors.Hand;
        ContextMenu = null;
    }

    public Hotkey? Hotkey
    {
        get => (Hotkey?)GetValue(HotkeyProperty);
        set => SetValue(HotkeyProperty, value);
    }

    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnGotKeyboardFocus(e);
        Text = "Press a shortcut…";
    }

    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnLostKeyboardFocus(e);
        Text = Hotkey?.ToString() ?? string.Empty;
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        // Let Tab move focus; Escape keeps the current shortcut.
        if (key is Key.Tab || (key is Key.Escape && Keyboard.Modifiers == ModifierKeys.None))
        {
            base.OnPreviewKeyDown(e);
            return;
        }

        e.Handled = true;
        if (IsModifierKey(key))
        {
            return;
        }

        var candidate = new Hotkey(ToHotkeyModifiers(Keyboard.Modifiers), KeyInterop.VirtualKeyFromKey(key));
        if (candidate.IsValid)
        {
            Hotkey = candidate;
            Text = candidate.ToString();
        }
    }

    private static void OnHotkeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var box = (HotkeyBox)d;
        if (!box.IsKeyboardFocused)
        {
            box.Text = (e.NewValue as Hotkey)?.ToString() ?? string.Empty;
        }
    }

    private static bool IsModifierKey(Key key) => key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift
        or Key.RightShift or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin;

    private static HotkeyModifiers ToHotkeyModifiers(ModifierKeys modifiers)
    {
        var result = HotkeyModifiers.None;
        if (modifiers.HasFlag(ModifierKeys.Control)) result |= HotkeyModifiers.Control;
        if (modifiers.HasFlag(ModifierKeys.Shift)) result |= HotkeyModifiers.Shift;
        if (modifiers.HasFlag(ModifierKeys.Alt)) result |= HotkeyModifiers.Alt;
        if (modifiers.HasFlag(ModifierKeys.Windows)) result |= HotkeyModifiers.Windows;
        return result;
    }
}
