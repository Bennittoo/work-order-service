using WorkOrderService.Domain.Enumerations;

namespace WorkOrderService.Application.Models;

/// <summary>One entry in a work order's status trail.</summary>
/// <param name="FromStatus">The status held before this change. Null only on the creation entry.</param>
/// <param name="ToStatus">The status held after this change.</param>
/// <param name="OccurredAt">When the change happened according to whoever reported it.</param>
/// <param name="RecordedAt">When this service persisted the change.</param>
/// <param name="Source">Which entry point caused the change.</param>
/// <param name="Details">Free text supplied with the change.</param>
/// <param name="EventId">The progress event responsible, where there was one.</param>
public sealed record StatusHistoryEntryModel(
    WorkOrderStatus? FromStatus,
    WorkOrderStatus ToStatus,
    DateTimeOffset OccurredAt,
    DateTimeOffset RecordedAt,
    StatusChangeSource Source,
    string? Details,
    Guid? EventId);
