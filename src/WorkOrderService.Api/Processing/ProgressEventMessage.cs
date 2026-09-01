using WorkOrderService.Domain;

namespace WorkOrderService.Api.Processing;

/// <summary>
/// What travels on the queue. Deliberately not the HTTP request type: the wire contract and the
/// internal message are free to change independently, and nothing downstream can accidentally
/// depend on a nullable field that only existed to make validation possible.
/// </summary>
public sealed record ProgressEventMessage(
    Guid EventId,
    string WorkOrderExternalId,
    WorkOrderStatus NewStatus,
    DateTimeOffset OccurredAt,
    string? Details);
