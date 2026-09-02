using WorkOrderService.Domain.Enumerations;

namespace WorkOrderService.Domain;

/// <summary>
/// One entry in a work order's status trail. Append-only: entries are written by
/// <see cref="WorkOrder.ApplyStatus"/> and never modified afterwards.
/// </summary>
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

    /// <summary>Identifier of this entry.</summary>
    public Guid Id { get; private set; }

    /// <summary>The work order this entry belongs to.</summary>
    public Guid WorkOrderId { get; private set; }

    /// <summary>The status held before this change. Null only on the creation entry, which has no prior status.</summary>
    public WorkOrderStatus? FromStatus { get; private set; }

    /// <summary>The status held after this change.</summary>
    public WorkOrderStatus ToStatus { get; private set; }

    /// <summary>
    /// When the change happened according to whoever reported it. Taken from the progress event where
    /// there was one, so an event that arrived late stays distinguishable from one that was processed
    /// late. Equal to <see cref="RecordedAt"/> for changes made through the API.
    /// </summary>
    public DateTimeOffset OccurredAt { get; private set; }

    /// <summary>When this service persisted the change.</summary>
    public DateTimeOffset RecordedAt { get; private set; }

    /// <summary>Which entry point caused the change.</summary>
    public StatusChangeSource Source { get; private set; }

    /// <summary>Free text supplied with the change, such as why a work order was cancelled.</summary>
    public string? Details { get; private set; }

    /// <summary>The progress event that caused this entry, where there was one. Null for API and creation entries.</summary>
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
