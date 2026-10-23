using System.Windows.Threading;

namespace ScreenRecorder.App.Services;

/// <summary>
/// Marshals work onto the UI thread so view models never touch <see cref="Dispatcher"/> directly.
/// </summary>
public interface IUiDispatcher
{
    /// <summary>Queues <paramref name="action"/> to run on the UI thread, preserving call order.</summary>
    void Post(Action action);
}

public sealed class WpfUiDispatcher(Dispatcher dispatcher) : IUiDispatcher
{
    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        dispatcher.BeginInvoke(action);
    }
}
