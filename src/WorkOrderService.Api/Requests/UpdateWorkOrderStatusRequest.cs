using WorkOrderService.Application.Validations;
using WorkOrderService.Domain.Enumerations;

namespace WorkOrderService.Api.Requests;

/// <summary>A request to change a work order status.</summary>
/// <param name="Status">The status to move to. Sent as a name, such as <c>InProgress</c>.</param>
/// <param name="Details">Optional free text stored against the change, such as why it was cancelled.</param>
public sealed record UpdateWorkOrderStatusRequest(WorkOrderStatus Status, string? Details)
    : IValidatableRequest
{
    /// <inheritdoc />
    public IDictionary<string, string[]> Validate() =>
        WorkOrderValidator.ValidateStatusChange(Status, Details);
}
