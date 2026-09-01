using WorkOrderService.Api.Persistence;
using WorkOrderService.Api.Validation;

namespace WorkOrderService.Api.Contracts;

public sealed record CreateWorkOrderRequest(string? ExternalId, string? SiteCode, string? Description)
    : IValidatableRequest
{
    public IDictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();

        Check(errors, nameof(ExternalId), ExternalId, FieldLengths.ExternalId);
        Check(errors, nameof(SiteCode), SiteCode, FieldLengths.SiteCode);
        Check(errors, nameof(Description), Description, FieldLengths.Description);

        return errors;
    }

    private static void Check(IDictionary<string, string[]> errors, string field, string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors[field] = ["A value is required."];
        }
        else if (value.Trim().Length > maxLength)
        {
            errors[field] = [$"Must be {maxLength} characters or fewer."];
        }
    }
}
