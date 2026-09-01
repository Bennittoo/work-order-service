namespace WorkOrderService.Domain;

public sealed class StatusHistoryEntry
{
    private StatusHistoryEntry()
    {
    }

    private StatusHistoryEntry(
        Guid workOrderId,
        WorkOrderStatus? fromStatus,
        WorkOrderStatus toStatus,
        DateTimeOffset occurredAt,
        DateTimeOffset recordedAt,
        StatusChangeSource source,
        string? details,
        Guid? eventId)
    {
        Id = Guid.NewGuid();
        WorkOrderId = workOrderId;
        FromStatus = fromStatus;
        ToStatus = toStatus;
        OccurredAt = occurredAt;
        RecordedAt = recordedAt;
        Source = source;
        Details = details;
        EventId = eventId;
    }

    public Guid Id { get; private set; }

    public Guid WorkOrderId { get; private set; }

    /// <summary>Null only on the creation entry, which has no prior status.</summary>
    public WorkOrderStatus? FromStatus { get; private set; }

    public WorkOrderStatus ToStatus { get; private set; }

    /// <summary>
    /// When the change happened according to whoever reported it. Taken from the progress event
    /// where there was one, so an event that arrived late stays distinguishable from one that was
    /// processed late. Equal to <see cref="RecordedAt"/> for changes made through the API.
    /// </summary>
    public DateTimeOffset OccurredAt { get; private set; }

    /// <summary>When this service persisted the change.</summary>
    public DateTimeOffset RecordedAt { get; private set; }

    public StatusChangeSource Source { get; private set; }

    public string? Details { get; private set; }

    /// <summary>Correlates the entry back to the progress event that caused it, where there was one.</summary>
    public Guid? EventId { get; private set; }

    internal static StatusHistoryEntry ForCreation(Guid workOrderId, DateTimeOffset at) =>
        new(workOrderId, null, StatusTransitions.Initial, at, at, StatusChangeSource.Creation, null, null);

    internal static StatusHistoryEntry ForChange(
        Guid workOrderId,
        WorkOrderStatus fromStatus,
        WorkOrderStatus toStatus,
        DateTimeOffset occurredAt,
        DateTimeOffset recordedAt,
        StatusChangeSource source,
        string? details,
        Guid? eventId) =>
        new(workOrderId, fromStatus, toStatus, occurredAt, recordedAt, source, details, eventId);
}
