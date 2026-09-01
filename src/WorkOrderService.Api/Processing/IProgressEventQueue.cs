namespace WorkOrderService.Api.Processing;

public interface IProgressEventQueue
{
    int Capacity { get; }

    /// <summary>
    /// Adds an event if there is room. Returns false immediately when the buffer is full rather than
    /// waiting, so a burst becomes a fast rejection the caller can retry rather than an unbounded
    /// request latency.
    /// </summary>
    bool TryEnqueue(ProgressEventMessage message);

    IAsyncEnumerable<ProgressEventMessage> DequeueAllAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Stops accepting new events and lets a reader finish what is already buffered. Called on
    /// shutdown so the queue drains instead of being abandoned.
    /// </summary>
    void CompleteAdding();
}
