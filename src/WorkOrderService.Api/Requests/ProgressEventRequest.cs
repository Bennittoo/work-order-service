using WorkOrderService.Application.Validations;
using WorkOrderService.Domain.Enumerations;

namespace WorkOrderService.Api.Requests;

/// <summary>A progress event reported by an external system.</summary>
/// <param name="EventId">
/// The sender's identifier for this event. Required, and the deduplication key: resubmitting the
/// same identifier produces no further effects.
/// </param>
/// <param name="WorkOrderExternalId">The external identifier of the work order being reported on. Required.</param>
/// <param name="NewStatus">The status being reported, sent as a name such as <c>InProgress</c>.</param>
/// <param name="OccurredAt">When the change happened according to the sender, which may be earlier than now.</param>
/// <param name="Details">Optional free text describing the change.</param>
public sealed record ProgressEventRequest(
    Guid EventId,
    string? WorkOrderExternalId,
    WorkOrderStatus NewStatus,
    DateTimeOffset OccurredAt,
    string? Details) : IValidatableRequest
{
    /// <inheritdoc />
    public IDictionary<string, string[]> Validate() =>
        ProgressEventValidator.Validate(EventId, WorkOrderExternalId, NewStatus, OccurredAt, Details);
}
