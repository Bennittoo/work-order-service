using WorkOrderService.Domain.Enumerations;

namespace WorkOrderService.Application.Validations;

/// <summary>Rules for the inbound work order requests.</summary>
public static class WorkOrderValidator
{
    /// <summary>Validates a request to create a work order.</summary>
    /// <param name="externalId">The upstream system's key.</param>
    /// <param name="siteCode">The site the work is at.</param>
    /// <param name="description">What the work is.</param>
    /// <returns>Field name to messages. Empty when valid.</returns>
    public static IDictionary<string, string[]> ValidateCreate(
        string? externalId, string? siteCode, string? description)
    {
        var errors = ValidationErrors.None();

        errors.RequiredText("ExternalId", externalId, FieldLengths.ExternalId);
        errors.RequiredText("SiteCode", siteCode, FieldLengths.SiteCode);
        errors.RequiredText("Description", description, FieldLengths.Description);

        return errors;
    }

    /// <summary>Validates a request to change a work order status.</summary>
    /// <param name="status">The proposed status.</param>
    /// <param name="details">Optional free text to store against the change.</param>
    /// <returns>Field name to messages. Empty when valid.</returns>
    public static IDictionary<string, string[]> ValidateStatusChange(WorkOrderStatus status, string? details)
    {
        var errors = ValidationErrors.None();

        if (!Enum.IsDefined(status))
        {
            errors.Add("Status", UnknownStatusMessage());
        }

        if (details is not null && details.Length > FieldLengths.Details)
        {
            errors.TooLong("Details", FieldLengths.Details);
        }

        return errors;
    }

    /// <summary>Validates and parses the list query string.</summary>
    /// <param name="status">The optional status filter, as supplied. Matched case-insensitively.</param>
    /// <param name="page">The requested one-based page number.</param>
    /// <returns>The parsed filter alongside any failures.</returns>
    public static ListQueryValidation ValidateListQuery(string? status, int page)
    {
        var errors = ValidationErrors.None();
        WorkOrderStatus? filter = null;

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (Enum.TryParse<WorkOrderStatus>(status, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
            {
                filter = parsed;
            }
            else
            {
                errors.Add("status", UnknownStatusMessage());
            }
        }

        if (page < 1)
        {
            errors.Add("page", "Must be 1 or greater.");
        }

        return new ListQueryValidation(errors, filter, page);
    }

    internal static string UnknownStatusMessage() =>
        $"Unknown status. Expected one of: {string.Join(", ", Enum.GetNames<WorkOrderStatus>())}.";
}
