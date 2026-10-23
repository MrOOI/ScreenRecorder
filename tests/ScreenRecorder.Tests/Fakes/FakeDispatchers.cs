using ScreenRecorder.App.Services;

namespace ScreenRecorder.Tests.Fakes;

/// <summary>Runs posted work synchronously.</summary>
internal sealed class ImmediateDispatcher : IUiDispatcher
{
    public void Post(Action action) => action();
}

/// <summary>Queues posted work until <see cref="RunAll"/>, like a real UI thread between messages.</summary>
internal sealed class QueuedDispatcher : IUiDispatcher
{
    private readonly Queue<Action> _queue = new();

    public void Post(Action action) => _queue.Enqueue(action);

    public void RunAll()
    {
        while (_queue.TryDequeue(out var action))
        {
            action();
        }
    }
}
