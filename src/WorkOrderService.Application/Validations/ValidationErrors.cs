namespace WorkOrderService.Application.Validations;

/// <summary>Builds the field-to-messages dictionary the validators return.</summary>
public static class ValidationErrors
{
    /// <summary>An empty, writable error set.</summary>
    public static Dictionary<string, string[]> None() => new();

    /// <summary>Adds a required-value failure for a field.</summary>
    /// <param name="errors">The set being built.</param>
    /// <param name="field">The field name to report against.</param>
    public static void Required(this IDictionary<string, string[]> errors, string field) =>
        errors[field] = ["A value is required."];

    /// <summary>Adds a too-long failure for a field.</summary>
    /// <param name="errors">The set being built.</param>
    /// <param name="field">The field name to report against.</param>
    /// <param name="maxLength">The maximum permitted length.</param>
    public static void TooLong(this IDictionary<string, string[]> errors, string field, int maxLength) =>
        errors[field] = [$"Must be {maxLength} characters or fewer."];

    /// <summary>Adds an arbitrary failure message for a field.</summary>
    /// <param name="errors">The set being built.</param>
    /// <param name="field">The field name to report against.</param>
    /// <param name="message">The message to report.</param>
    public static void Add(this IDictionary<string, string[]> errors, string field, string message) =>
        errors[field] = [message];

    /// <summary>Requires a present value no longer than <paramref name="maxLength"/>.</summary>
    /// <param name="errors">The set being built.</param>
    /// <param name="field">The field name to report against.</param>
    /// <param name="value">The supplied value.</param>
    /// <param name="maxLength">The maximum permitted length.</param>
    public static void RequiredText(
        this IDictionary<string, string[]> errors, string field, string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Required(field);
        }
        else if (value.Trim().Length > maxLength)
        {
            errors.TooLong(field, maxLength);
        }
    }
}
