using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WorkOrderService.Application.Abstractions;
using WorkOrderService.Application.Managers;
using WorkOrderService.Application.Models;
using WorkOrderService.Application.Options;

namespace WorkOrderService.Api.Processing;

/// <summary>
/// Single consumer of the progress event queue.
/// </summary>
/// <remarks>
/// One consumer serialises every event driven change, which removes lost updates between events
/// without per-work-order locking. The optimistic concurrency token still earns its place, because
/// the HTTP status endpoint is a second writer.
/// <para>
/// This type owns delivery concerns only: scoping, retrying and logging. What to do with an event is
/// the manager's decision.
/// </para>
/// </remarks>
public sealed class ProgressEventProcessor : BackgroundService
{
    private readonly IProgressEventQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ProgressEventOptions _options;
    private readonly ILogger<ProgressEventProcessor> _logger;

    /// <summary>Creates the processor.</summary>
    /// <param name="queue">The queue to consume.</param>
    /// <param name="scopeFactory">Creates a dependency injection scope per event.</param>
    /// <param name="options">Supplies the retry limit.</param>
    /// <param name="logger">Receives processing outcomes.</param>
    public ProgressEventProcessor(
        IProgressEventQueue queue,
        IServiceScopeFactory scopeFactory,
        IOptions<ProgressEventOptions> options,
        ILogger<ProgressEventProcessor> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        // Close the queue before the base class signals the stopping token, so the loop below sees
        // completion and drains what was already accepted rather than abandoning it.
        _queue.CompleteAdding();
        await base.StopAsync(cancellationToken);
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Progress event processor started with a queue capacity of {Capacity}.", _queue.Capacity);

        // The stopping token is deliberately not passed to the reader. Shutdown completes the writer
        // instead, so buffered events are processed rather than dropped. Anything still queued when
        // the host shutdown timeout expires is lost, which is the accepted cost of an in-memory queue.
        await foreach (var message in _queue.DequeueAllAsync(CancellationToken.None))
        {
            using var scope = _logger.BeginScope(new Dictionary<string, object>
            {
                ["EventId"] = message.EventId,
                ["WorkOrderExternalId"] = message.WorkOrderExternalId
            });

            try
            {
                await ProcessAsync(message);
            }
            catch (Exception exception)
            {
                // Per item, not around the loop. An exception escaping ExecuteAsync stops the hosted
                // service, which would silently end all event processing for the life of the process.
                _logger.LogError(exception, "Progress event failed and will not be retried.");
            }
        }

        _logger.LogInformation("Progress event processor drained and stopped.");
    }

    private async Task ProcessAsync(ProgressEventMessage message)
    {
        for (var attempt = 1; attempt <= _options.MaxProcessingAttempts; attempt++)
        {
            // The processor is a singleton and the manager holds a scoped DbContext, so each event is
            // handled inside its own scope.
            using var scope = _scopeFactory.CreateScope();
            var manager = scope.ServiceProvider.GetRequiredService<WorkOrderServiceManager>();

            try
            {
                if (await manager.ApplyProgressEventAsync(message) is { } outcome)
                {
                    _logger.LogInformation("Progress event processed with outcome {Outcome}.", outcome);
                }
                else
                {
                    _logger.LogInformation("Progress event already processed; duplicate ignored.");
                }

                return;
            }
            catch (DbUpdateConcurrencyException) when (attempt < _options.MaxProcessingAttempts)
            {
                // Someone else wrote the work order between the read and the write. Re-reading and
                // re-evaluating the transition against current state is the correct response, not
                // forcing the write through.
                _logger.LogWarning(
                    "Work order changed during processing; re-reading (attempt {Attempt}).", attempt);
            }
        }

        _logger.LogError(
            "Giving up on progress event after {Attempts} concurrency conflicts.",
            _options.MaxProcessingAttempts);
    }
}
