using WorkOrderService.Domain.Enumerations;

namespace WorkOrderService.Application.Validations;

/// <summary>The outcome of validating the work order list query string.</summary>
/// <param name="Errors">Field name to messages. Empty when valid.</param>
/// <param name="Status">The parsed status filter, or null when no filter was supplied.</param>
/// <param name="Page">The requested one-based page number.</param>
public sealed record ListQueryValidation(
    IDictionary<string, string[]> Errors,
    WorkOrderStatus? Status,
    int Page);
