using WorkOrderService.Api.Persistence;
using WorkOrderService.Api.Validation;
using WorkOrderService.Domain;

namespace WorkOrderService.Api.Contracts;

public sealed record ProgressEventRequest(
    Guid EventId,
    string? WorkOrderExternalId,
    WorkOrderStatus NewStatus,
    DateTimeOffset OccurredAt,
    string? Details) : IValidatableRequest
{
    public IDictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();

        if (EventId == Guid.Empty)
        {
            // The identifier is the deduplication key, so an absent one would make the event
            // impossible to recognise on redelivery.
            errors[nameof(EventId)] = ["A non-empty event identifier is required."];
        }

        if (string.IsNullOrWhiteSpace(WorkOrderExternalId))
        {
            errors[nameof(WorkOrderExternalId)] = ["A value is required."];
        }
        else if (WorkOrderExternalId.Trim().Length > FieldLengths.ExternalId)
        {
            errors[nameof(WorkOrderExternalId)] = [$"Must be {FieldLengths.ExternalId} characters or fewer."];
        }

        if (!Enum.IsDefined(NewStatus))
        {
            errors[nameof(NewStatus)] =
                [$"Unknown status. Expected one of: {string.Join(", ", Enum.GetNames<WorkOrderStatus>())}."];
        }

        if (OccurredAt == default)
        {
            errors[nameof(OccurredAt)] = ["A timestamp is required."];
        }

        if (Details is not null && Details.Length > FieldLengths.Details)
        {
            errors[nameof(Details)] = [$"Must be {FieldLengths.Details} characters or fewer."];
        }

        return errors;
    }
}

public sealed record ProgressEventAcceptedResponse(Guid EventId, string Status);
