namespace ScreenRecorder.Core.Models;

/// <summary>
/// Modifier keys; values match the Win32 <c>MOD_*</c> constants.
/// </summary>
[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 0x1,
    Control = 0x2,
    Shift = 0x4,
    Windows = 0x8,
}

/// <summary>
/// The actions that can be bound to a global hotkey.
/// </summary>
public enum HotkeyAction
{
    StartStop,
    PauseResume,
}

/// <summary>
/// A global key combination. <see cref="VirtualKey"/> is a Win32 virtual-key code.
/// </summary>
public sealed record Hotkey(HotkeyModifiers Modifiers, int VirtualKey)
{
    private const int VkF1 = 0x70;
    private const int VkF24 = 0x87;

    /// <summary>A hotkey needs a modifier so ordinary typing never triggers it.</summary>
    public bool IsValid => Modifiers != HotkeyModifiers.None && VirtualKey > 0;

    public override string ToString()
    {
        var parts = new List<string>(5);
        if (Modifiers.HasFlag(HotkeyModifiers.Control)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(HotkeyModifiers.Shift)) parts.Add("Shift");
        if (Modifiers.HasFlag(HotkeyModifiers.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(HotkeyModifiers.Windows)) parts.Add("Win");
        parts.Add(KeyName(VirtualKey));
        return string.Join("+", parts);
    }

    private static string KeyName(int vk) => vk switch
    {
        (>= '0' and <= '9') or (>= 'A' and <= 'Z') => ((char)vk).ToString(),
        >= VkF1 and <= VkF24 => $"F{vk - VkF1 + 1}",
        _ => $"Key{vk}",
    };
}
