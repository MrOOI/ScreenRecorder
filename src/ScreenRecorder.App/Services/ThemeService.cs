using System.Windows;
using Microsoft.Win32;
using ScreenRecorder.App.Interop;

namespace ScreenRecorder.App.Services;

/// <summary>
/// Follows the Windows app theme (light or dark) once at startup and applies it to every window.
/// </summary>
public sealed class ThemeService
{
    public const string ThemeOverrideVariable = "SCREENRECORDER_THEME";

    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    public bool IsDark { get; private set; }

    public void Apply(Application application)
    {
        IsDark = ReadSystemPrefersDark();

        var tokens = new ResourceDictionary
        {
            Source = new Uri($"pack://application:,,,/Themes/{(IsDark ? "Dark" : "Light")}.xaml", UriKind.Absolute),
        };
        application.Resources.MergedDictionaries.Insert(0, tokens);

        // One class handler gives every window, including dialogs, a title bar matching the theme.
        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent,
            new RoutedEventHandler((sender, _) => WindowInterop.SetDarkTitleBar((Window)sender, IsDark)));
    }

    private static bool ReadSystemPrefersDark()
    {
        // Developer override for previews and screenshots: SCREENRECORDER_THEME=dark|light.
        var forced = Environment.GetEnvironmentVariable(ThemeOverrideVariable);
        if (string.Equals(forced, "dark", StringComparison.OrdinalIgnoreCase)) return true;
        if (string.Equals(forced, "light", StringComparison.OrdinalIgnoreCase)) return false;

        using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
        return key?.GetValue("AppsUseLightTheme") is int useLight && useLight == 0;
    }
}
