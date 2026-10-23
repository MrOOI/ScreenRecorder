using ScreenRecorder.Core.Abstractions;
using ScreenRecorder.Core.Models;

namespace ScreenRecorder.Core.Services;

/// <summary>
/// Holds the current settings in memory and writes every change through to the store.
/// </summary>
public sealed class SettingsService : ISettingsService
{
    private readonly ISettingsStore _store;
    private readonly object _gate = new();
    private AppSettings _current;

    public SettingsService(ISettingsStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _current = store.Load().Normalize();
    }

    public event EventHandler<SettingsChangedEventArgs>? Changed;

    public AppSettings Current
    {
        get { lock (_gate) return _current; }
    }

    public void Update(Func<AppSettings, AppSettings> change)
    {
        ArgumentNullException.ThrowIfNull(change);

        AppSettings oldSettings;
        AppSettings newSettings;
        lock (_gate)
        {
            oldSettings = _current;
            newSettings = change(oldSettings).Normalize();
            if (newSettings == oldSettings)
            {
                return;
            }

            _current = newSettings;
            _store.Save(newSettings);
        }

        Changed?.Invoke(this, new SettingsChangedEventArgs(oldSettings, newSettings));
    }
}
