using WorkOrderService.Domain;
using WorkOrderService.Domain.Enumerations;

namespace WorkOrderService.Application.Models;

/// <summary>A work order with its full status trail.</summary>
/// <param name="Id">Identifier assigned by this service.</param>
/// <param name="ExternalId">The upstream system's key for this work order.</param>
/// <param name="SiteCode">The site the work is at.</param>
/// <param name="Description">What the work is.</param>
/// <param name="Status">Current lifecycle position.</param>
/// <param name="CreatedAt">When the work order was created.</param>
/// <param name="UpdatedAt">When the status last changed.</param>
/// <param name="StatusHistory">The status trail, oldest first.</param>
public sealed record WorkOrderModel(
    Guid Id,
    string ExternalId,
    string SiteCode,
    string Description,
    WorkOrderStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<StatusHistoryEntryModel> StatusHistory)
{
    /// <summary>Maps a tracked work order, ordering the trail by when each change was recorded.</summary>
    /// <param name="workOrder">The entity to map. Its history must already be loaded.</param>
    public static WorkOrderModel FromEntity(WorkOrder workOrder) =>
        new(workOrder.Id,
            workOrder.ExternalId,
            workOrder.SiteCode,
            workOrder.Description,
            workOrder.Status,
            workOrder.CreatedAt,
            workOrder.UpdatedAt,
            workOrder.StatusHistory
                .OrderBy(entry => entry.RecordedAt)
                .Select(entry => new StatusHistoryEntryModel(
                    entry.FromStatus,
                    entry.ToStatus,
                    entry.OccurredAt,
                    entry.RecordedAt,
                    entry.Source,
                    entry.Details,
                    entry.EventId))
                .ToList());
}
