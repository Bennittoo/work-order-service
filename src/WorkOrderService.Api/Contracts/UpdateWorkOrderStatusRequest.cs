using WorkOrderService.Api.Persistence;
using WorkOrderService.Api.Validation;
using WorkOrderService.Domain;

namespace WorkOrderService.Api.Contracts;

public sealed record UpdateWorkOrderStatusRequest(WorkOrderStatus Status, string? Details)
    : IValidatableRequest
{
    public IDictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();

        if (!Enum.IsDefined(Status))
        {
            errors[nameof(Status)] =
                [$"Unknown status. Expected one of: {string.Join(", ", Enum.GetNames<WorkOrderStatus>())}."];
        }

        if (Details is not null && Details.Length > FieldLengths.Details)
        {
            errors[nameof(Details)] = [$"Must be {FieldLengths.Details} characters or fewer."];
        }

        return errors;
    }
}
