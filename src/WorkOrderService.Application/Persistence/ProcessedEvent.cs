using WorkOrderService.Application.Enumerations;

namespace WorkOrderService.Application.Persistence;

/// <summary>
/// One row per progress event the worker has finished with.
/// </summary>
/// <remarks>
/// The unique key on <see cref="EventId"/> is what makes processing idempotent, and the row is
/// written in the same transaction as the change it describes, so an event is only ever marked
/// handled if its effect actually committed.
/// <para>
/// There is deliberately no foreign key to the work order. An event naming an unknown work order
/// still has to be recorded, or it would be retried forever, and a constraint would make that row
/// impossible to write.
/// </para>
/// </remarks>
public sealed class ProcessedEvent
{
    private ProcessedEvent()
    {
    }

    private ProcessedEvent(
        Guid eventId,
        string workOrderExternalId,
        Guid? workOrderId,
        ProcessedEventOutcome outcome,
        string? detail,
        DateTimeOffset occurredAt,
        DateTimeOffset processedAt)
    {
        EventId = eventId;
        WorkOrderExternalId = workOrderExternalId;
        WorkOrderId = workOrderId;
        Outcome = outcome;
        Detail = detail;
        OccurredAt = occurredAt;
        ProcessedAt = processedAt;
    }

    /// <summary>The sender's identifier for the event. The deduplication key, and the primary key.</summary>
    public Guid EventId { get; private set; }

    /// <summary>The identifier the event used to address the work order, kept even when it matched nothing.</summary>
    public string WorkOrderExternalId { get; private set; } = string.Empty;

    /// <summary>The work order the event was applied to. Null when the event named one that does not exist.</summary>
    public Guid? WorkOrderId { get; private set; }

    /// <summary>What was done with the event.</summary>
    public ProcessedEventOutcome Outcome { get; private set; }

    /// <summary>Why the outcome was what it was. Carries the rejection reason where there is one.</summary>
    public string? Detail { get; private set; }

    /// <summary>When the sender said the change happened.</summary>
    public DateTimeOffset OccurredAt { get; private set; }

    /// <summary>When this service finished with the event.</summary>
    public DateTimeOffset ProcessedAt { get; private set; }

    /// <summary>Records an event as handled.</summary>
    /// <param name="eventId">The sender's identifier for the event.</param>
    /// <param name="workOrderExternalId">The identifier the event used to address the work order.</param>
    /// <param name="workOrderId">The work order it was applied to, if any.</param>
    /// <param name="outcome">What was done with the event.</param>
    /// <param name="detail">Why the outcome was what it was.</param>
    /// <param name="occurredAt">When the sender said the change happened.</param>
    /// <param name="processedAt">When this service finished with it.</param>
    public static ProcessedEvent Handled(
        Guid eventId,
        string workOrderExternalId,
        Guid? workOrderId,
        ProcessedEventOutcome outcome,
        string? detail,
        DateTimeOffset occurredAt,
        DateTimeOffset processedAt) =>
        new(eventId, workOrderExternalId, workOrderId, outcome, detail, occurredAt, processedAt);
}
