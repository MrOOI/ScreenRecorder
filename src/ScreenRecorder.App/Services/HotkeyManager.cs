using Microsoft.Extensions.Logging;
using ScreenRecorder.Core.Abstractions;
using ScreenRecorder.Core.Models;

namespace ScreenRecorder.App.Services;

/// <summary>
/// Keeps global hotkey registrations in sync with settings and reports conflicts to the user.
/// </summary>
public sealed class HotkeyManager : IDisposable
{
    private readonly IHotkeyService _hotkeys;
    private readonly ISettingsService _settings;
    private readonly INotificationService _notifications;
    private readonly ILogger<HotkeyManager> _logger;
    private int _suspendCount;

    public HotkeyManager(
        IHotkeyService hotkeys,
        ISettingsService settings,
        INotificationService notifications,
        ILogger<HotkeyManager> logger)
    {
        _hotkeys = hotkeys;
        _settings = settings;
        _notifications = notifications;
        _logger = logger;
    }

    /// <summary>Raised on the UI thread when a bound hotkey is pressed.</summary>
    public event EventHandler<HotkeyPressedEventArgs>? Triggered;

    public void Start()
    {
        _hotkeys.Pressed += OnPressed;
        _settings.Changed += OnSettingsChanged;
        RegisterAll();
    }

    /// <summary>Temporarily releases all hotkeys; they are restored from current settings on dispose.</summary>
    public IDisposable Suspend()
    {
        if (_suspendCount++ == 0)
        {
            _hotkeys.UnregisterAll();
        }

        return new Resumer(this);
    }

    public void Dispose()
    {
        _hotkeys.Pressed -= OnPressed;
        _settings.Changed -= OnSettingsChanged;
        _hotkeys.UnregisterAll();
    }

    private void Resume()
    {
        if (--_suspendCount == 0)
        {
            RegisterAll();
        }
    }

    private void OnSettingsChanged(object? sender, SettingsChangedEventArgs e)
    {
        var changed = e.OldSettings.StartStopHotkey != e.NewSettings.StartStopHotkey
                      || e.OldSettings.PauseResumeHotkey != e.NewSettings.PauseResumeHotkey;
        if (changed && _suspendCount == 0)
        {
            RegisterAll();
        }
    }

    private void RegisterAll()
    {
        var settings = _settings.Current;
        var conflicts = new List<Hotkey>();

        if (!_hotkeys.TryRegister(HotkeyAction.StartStop, settings.StartStopHotkey))
        {
            conflicts.Add(settings.StartStopHotkey);
        }

        if (!_hotkeys.TryRegister(HotkeyAction.PauseResume, settings.PauseResumeHotkey))
        {
            conflicts.Add(settings.PauseResumeHotkey);
        }

        if (conflicts.Count > 0)
        {
            _logger.LogWarning("Hotkeys unavailable: {Hotkeys}", string.Join(", ", conflicts));
            var verb = conflicts.Count == 1 ? "is" : "are";
            _notifications.ShowError(
                $"{string.Join(" and ", conflicts)} {verb} already used by another app. Pick a different shortcut in Settings.");
        }
    }

    private void OnPressed(object? sender, HotkeyPressedEventArgs e) => Triggered?.Invoke(this, e);

    private sealed class Resumer(HotkeyManager owner) : IDisposable
    {
        private HotkeyManager? _owner = owner;

        public void Dispose() => Interlocked.Exchange(ref _owner, null)?.Resume();
    }
}
