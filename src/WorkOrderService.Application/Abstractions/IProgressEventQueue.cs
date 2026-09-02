using WorkOrderService.Application.Models;

namespace WorkOrderService.Application.Abstractions;

/// <summary>
/// The buffer between accepting a progress event and processing it.
/// </summary>
/// <remarks>
/// Three members and no knowledge of how the queue is implemented, so swapping the in-memory
/// channel for a real broker is a registration change rather than a rewrite.
/// </remarks>
public interface IProgressEventQueue
{
    /// <summary>How many events may be waiting at once.</summary>
    int Capacity { get; }

    /// <summary>
    /// Adds an event if there is room. Returns false immediately when the buffer is full rather than
    /// waiting, so a burst becomes a fast rejection the caller can retry rather than an unbounded
    /// request latency.
    /// </summary>
    /// <param name="message">The event to enqueue.</param>
    bool TryEnqueue(ProgressEventMessage message);

    /// <summary>Yields queued events until the queue is completed.</summary>
    /// <param name="cancellationToken">Stops the enumeration.</param>
    IAsyncEnumerable<ProgressEventMessage> DequeueAllAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Stops accepting new events and lets a reader finish what is already buffered. Called on
    /// shutdown so the queue drains instead of being abandoned.
    /// </summary>
    void CompleteAdding();
}
