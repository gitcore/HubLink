namespace HubLink.Shared;

public abstract class TriggerQueue<T>(string tag) : ITriggerQueue<T> {

    private readonly ConcurrentQueue<T> _workItems = new();
    private readonly SemaphoreSlim _signal = new(0);

    public string Tag { get; } = tag;

    protected void Enqueue(T workItem) {
        ArgumentNullException.ThrowIfNull(workItem);

        _workItems.Enqueue(workItem);
        _signal.Release();
    }

    async Task<T> ITriggerQueue<T>.DequeueAsync(CancellationToken cancellationToken) {
        await _signal.WaitAsync(cancellationToken);
        _workItems.TryDequeue(out var workItem);

        return workItem;
    }
}
