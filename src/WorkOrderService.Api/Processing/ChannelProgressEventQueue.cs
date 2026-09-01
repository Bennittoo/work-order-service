using System.Threading.Channels;
using Microsoft.Extensions.Options;

namespace WorkOrderService.Api.Processing;

public sealed class ChannelProgressEventQueue : IProgressEventQueue
{
    private readonly Channel<ProgressEventMessage> _channel;

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

    public int Capacity { get; }

    public bool TryEnqueue(ProgressEventMessage message) => _channel.Writer.TryWrite(message);

    public IAsyncEnumerable<ProgressEventMessage> DequeueAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);

    public void CompleteAdding() => _channel.Writer.TryComplete();
}
