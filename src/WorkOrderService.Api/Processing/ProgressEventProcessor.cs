using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WorkOrderService.Api.Persistence;
using WorkOrderService.Domain;

namespace WorkOrderService.Api.Processing;

/// <summary>
/// Single consumer of the progress event queue. One consumer serialises every event driven change,
/// which removes lost updates between events without per-work-order locking. The optimistic
/// concurrency token still earns its place, because the HTTP status endpoint is a second writer.
/// </summary>
public sealed class ProgressEventProcessor : BackgroundService
{
    private readonly IProgressEventQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IUniqueConstraintDetector _uniqueConstraints;
    private readonly TimeProvider _clock;
    private readonly ProgressEventOptions _options;
    private readonly ILogger<ProgressEventProcessor> _logger;

    public ProgressEventProcessor(
        IProgressEventQueue queue,
        IServiceScopeFactory scopeFactory,
        IUniqueConstraintDetector uniqueConstraints,
        TimeProvider clock,
        IOptions<ProgressEventOptions> options,
        ILogger<ProgressEventProcessor> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _uniqueConstraints = uniqueConstraints;
        _clock = clock;
        _options = options.Value;
        _logger = logger;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        // Close the queue before the base class signals the stopping token, so the loop below sees
        // completion and drains what was already accepted rather than abandoning it.
        _queue.CompleteAdding();
        await base.StopAsync(cancellationToken);
    }

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
            try
            {
                if (await ApplyAsync(message) is { } outcome)
                {
                    _logger.LogInformation("Progress event processed with outcome {Outcome}.", outcome);
                }

                return;
            }
            catch (DbUpdateException exception) when (_uniqueConstraints.IsUniqueViolation(exception))
            {
                // Reached only when a redelivery slips past the existence check in ApplyAsync,
                // which the check cannot prevent because it is a read before a write. The unique
                // key is the authority; the check only keeps the common case off this path.
                _logger.LogInformation("Progress event already processed; duplicate ignored on write.");
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

    /// <summary>
    /// Applies one event, returning null if it had already been handled. The work order change, its
    /// history entry and the record marking the event handled all go through a single
    /// <c>SaveChangesAsync</c>, so they share one transaction: an event is only ever marked
    /// processed if its effect actually committed.
    /// </summary>
    private async Task<ProcessedEventOutcome?> ApplyAsync(ProgressEventMessage message)
    {
        // The processor is a singleton and DbContext is scoped, so a scope per event is required.
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WorkOrderDbContext>();

        // Redelivery is ordinary traffic from an at-least-once source, so the common duplicate is
        // recognised with a read rather than by letting an insert fail. This is an optimisation, not
        // the guard: it cannot close the window between the read and the write, which is why the
        // unique key still has to be there and still has to be caught.
        if (await db.ProcessedEvents.AnyAsync(e => e.EventId == message.EventId))
        {
            _logger.LogInformation("Progress event already processed; duplicate ignored.");
            return null;
        }

        var workOrder = await db.WorkOrders
            .Include(w => w.StatusHistory)
            .FirstOrDefaultAsync(w => w.ExternalId == message.WorkOrderExternalId);

        var now = _clock.GetUtcNow();
        ProcessedEventOutcome outcome;
        string? detail;

        if (workOrder is null)
        {
            // Recorded rather than thrown away: without a row here the same unknown identifier would
            // be reprocessed on every redelivery, and there would be no trace that it ever arrived.
            outcome = ProcessedEventOutcome.WorkOrderNotFound;
            detail = $"No work order exists with external identifier '{message.WorkOrderExternalId}'.";
            _logger.LogWarning("Progress event names a work order that does not exist.");
        }
        else
        {
            var result = workOrder.ApplyStatus(
                message.NewStatus,
                StatusChangeSource.ProgressEvent,
                message.OccurredAt,
                now,
                message.Details,
                message.EventId);

            outcome = result.Outcome switch
            {
                StatusChangeOutcome.Applied => ProcessedEventOutcome.Applied,
                StatusChangeOutcome.NoOp => ProcessedEventOutcome.NoOp,
                _ => ProcessedEventOutcome.Rejected
            };
            detail = result.Reason;
        }

        db.ProcessedEvents.Add(ProcessedEvent.Handled(
            message.EventId,
            message.WorkOrderExternalId,
            workOrder?.Id,
            outcome,
            detail,
            message.OccurredAt,
            now));

        await db.SaveChangesAsync();

        return outcome;
    }
}
