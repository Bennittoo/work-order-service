namespace WorkOrderService.Api.Persistence;

/// <summary>
/// One row per progress event the worker has finished with. The unique key on
/// <see cref="EventId"/> is what makes processing idempotent, and the row is written in the same
/// transaction as the change it describes, so an event is only ever marked handled if its effect
/// actually committed.
/// </summary>
/// <remarks>
/// There is deliberately no foreign key to the work order. An event naming an unknown work order
/// still has to be recorded, or it would be retried forever, and a constraint would make that
/// impossible to write.
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

    public Guid EventId { get; private set; }

    /// <summary>The identifier the event used to address the work order, kept even when it matched nothing.</summary>
    public string WorkOrderExternalId { get; private set; } = string.Empty;

    /// <summary>Null when the event named a work order that does not exist.</summary>
    public Guid? WorkOrderId { get; private set; }

    public ProcessedEventOutcome Outcome { get; private set; }

    /// <summary>Why the outcome was what it was. Carries the rejection reason where there is one.</summary>
    public string? Detail { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public DateTimeOffset ProcessedAt { get; private set; }

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
