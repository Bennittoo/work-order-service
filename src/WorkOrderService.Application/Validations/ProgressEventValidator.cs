using WorkOrderService.Domain.Enumerations;

namespace WorkOrderService.Application.Validations;

/// <summary>Rules for an inbound progress event.</summary>
public static class ProgressEventValidator
{
    /// <summary>Validates a progress event submission.</summary>
    /// <param name="eventId">The sender's identifier for this event, used to deduplicate it.</param>
    /// <param name="workOrderExternalId">The external identifier of the work order being reported on.</param>
    /// <param name="newStatus">The status the sender is reporting.</param>
    /// <param name="occurredAt">When the sender says the change happened.</param>
    /// <param name="details">Optional free text describing the change.</param>
    /// <returns>Field name to messages. Empty when valid.</returns>
    public static IDictionary<string, string[]> Validate(
        Guid eventId,
        string? workOrderExternalId,
        WorkOrderStatus newStatus,
        DateTimeOffset occurredAt,
        string? details)
    {
        var errors = ValidationErrors.None();

        if (eventId == Guid.Empty)
        {
            // The identifier is the deduplication key, so an absent one would make the event
            // impossible to recognise on redelivery.
            errors.Add("EventId", "A non-empty event identifier is required.");
        }

        errors.RequiredText("WorkOrderExternalId", workOrderExternalId, FieldLengths.ExternalId);

        if (!Enum.IsDefined(newStatus))
        {
            errors.Add("NewStatus", WorkOrderValidator.UnknownStatusMessage());
        }

        if (occurredAt == default)
        {
            errors.Add("OccurredAt", "A timestamp is required.");
        }

        if (details is not null && details.Length > FieldLengths.Details)
        {
            errors.TooLong("Details", FieldLengths.Details);
        }

        return errors;
    }
}
