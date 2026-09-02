using WorkOrderService.Application.Validations;

namespace WorkOrderService.Api.Requests;

/// <summary>A request to create a work order.</summary>
/// <param name="ExternalId">The upstream system's key for this work order. Required, and must be unused.</param>
/// <param name="SiteCode">The site the work is at, for example <c>JHB-042</c>. Required.</param>
/// <param name="Description">What the work is. Required.</param>
public sealed record CreateWorkOrderRequest(string? ExternalId, string? SiteCode, string? Description)
    : IValidatableRequest
{
    /// <inheritdoc />
    public IDictionary<string, string[]> Validate() =>
        WorkOrderValidator.ValidateCreate(ExternalId, SiteCode, Description);
}
