using System.Threading.Channels;
using Microsoft.Extensions.Options;
using WorkOrderService.Application.Abstractions;
using WorkOrderService.Application.Models;
using WorkOrderService.Application.Options;

namespace WorkOrderService.Api.Processing;

/// <summary>
/// The in-memory adapter for the progress event queue, backed by a bounded channel.
/// </summary>
public sealed class ChannelProgressEventQueue : IProgressEventQueue
{
    private readonly Channel<ProgressEventMessage> _channel;

    /// <summary>Creates the queue at the configured capacity.</summary>
    /// <param name="options">Supplies the queue capacity.</param>
    public ChannelProgressEventQueue(IOptions<ProgressEventOptions> options)
    {
        Capacity = options.Value.QueueCapacity;

        _channel = Channel.CreateBounded<ProgressEventMessage>(new BoundedChannelOptions(Capacity)
        {
            // Wait is the mode that makes TryWrite fail when full instead of evicting an item. The
            // dropping modes would lose an event that has already been acknowledged with a 202.
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });
    }

    /// <inheritdoc />
    public int Capacity { get; }

    /// <inheritdoc />
    public bool TryEnqueue(ProgressEventMessage message) => _channel.Writer.TryWrite(message);

    /// <inheritdoc />
    public IAsyncEnumerable<ProgressEventMessage> DequeueAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);

    /// <inheritdoc />
    public void CompleteAdding() => _channel.Writer.TryComplete();
}
