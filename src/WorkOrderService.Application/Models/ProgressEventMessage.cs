using WorkOrderService.Domain.Enumerations;

namespace WorkOrderService.Application.Models;

/// <summary>
/// A progress event as it travels on the queue.
/// </summary>
/// <remarks>
/// Deliberately not the HTTP request type. The wire contract and the internal message can then
/// change independently, and nothing downstream depends on a nullable field that existed only to
/// make validation possible.
/// </remarks>
/// <param name="EventId">The sender's identifier for this event, and the deduplication key.</param>
/// <param name="WorkOrderExternalId">The external identifier of the work order being reported on.</param>
/// <param name="NewStatus">The status the sender is reporting.</param>
/// <param name="OccurredAt">When the sender says the change happened.</param>
/// <param name="Details">Optional free text describing the change.</param>
public sealed record ProgressEventMessage(
    Guid EventId,
    string WorkOrderExternalId,
    WorkOrderStatus NewStatus,
    DateTimeOffset OccurredAt,
    string? Details);
