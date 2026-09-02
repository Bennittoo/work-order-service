using WorkOrderService.Application.Enumerations;

namespace WorkOrderService.Application.Models;

/// <summary>The result of a create attempt.</summary>
/// <param name="Outcome">What happened.</param>
/// <param name="WorkOrder">The created work order, when the outcome is Created.</param>
/// <param name="Detail">Why the attempt failed, when it did.</param>
public sealed record CreateWorkOrderResult(
    CreateWorkOrderOutcome Outcome,
    WorkOrderModel? WorkOrder,
    string? Detail)
{
    /// <summary>The work order was created.</summary>
    /// <param name="workOrder">The created work order.</param>
    public static CreateWorkOrderResult Created(WorkOrderModel workOrder) =>
        new(CreateWorkOrderOutcome.Created, workOrder, null);

    /// <summary>The external identifier is already taken.</summary>
    /// <param name="externalId">The identifier that clashed.</param>
    public static CreateWorkOrderResult DuplicateExternalId(string externalId) =>
        new(CreateWorkOrderOutcome.DuplicateExternalId, null,
            $"A work order with external identifier '{externalId}' already exists.");
}
