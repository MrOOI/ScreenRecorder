namespace ScreenRecorder.App.Services;

/// <summary>
/// Ensures one running instance per user. A second launch signals the first to show itself, then exits.
/// </summary>
public sealed class SingleInstanceGuard : IDisposable
{
    private const string MutexName = @"Local\ScreenRecorder.SingleInstance";
    private const string ActivateEventName = @"Local\ScreenRecorder.Activate";

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activateEvent;
    private RegisteredWaitHandle? _registration;

    public SingleInstanceGuard()
    {
        _mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        IsFirstInstance = createdNew;
        _activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ActivateEventName);
    }

    public bool IsFirstInstance { get; }

    /// <summary>Asks the already running instance to bring its window forward.</summary>
    public void SignalFirstInstance() => _activateEvent.Set();

    /// <summary>Invokes <paramref name="onActivationRequested"/> on a thread-pool thread whenever another launch signals.</summary>
    public void ListenForActivation(Action onActivationRequested)
    {
        _registration = ThreadPool.RegisterWaitForSingleObject(
            _activateEvent, (_, _) => onActivationRequested(), null, Timeout.Infinite, executeOnlyOnce: false);
    }

    public void Dispose()
    {
        _registration?.Unregister(null);
        _activateEvent.Dispose();
        if (IsFirstInstance)
        {
            _mutex.ReleaseMutex();
        }

        _mutex.Dispose();
    }
}
