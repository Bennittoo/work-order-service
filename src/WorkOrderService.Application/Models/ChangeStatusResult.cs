using WorkOrderService.Application.Enumerations;

namespace WorkOrderService.Application.Models;

/// <summary>The result of a status change attempted through the API.</summary>
/// <param name="Outcome">What happened.</param>
/// <param name="WorkOrder">The work order as it now stands, when the outcome is Updated.</param>
/// <param name="Detail">Why the attempt did not apply, when it did not.</param>
public sealed record ChangeStatusResult(
    ChangeStatusOutcome Outcome,
    WorkOrderModel? WorkOrder,
    string? Detail)
{
    /// <summary>The work order is at the requested status.</summary>
    /// <param name="workOrder">The work order as it now stands.</param>
    public static ChangeStatusResult Updated(WorkOrderModel workOrder) =>
        new(ChangeStatusOutcome.Updated, workOrder, null);

    /// <summary>No work order exists with that identifier.</summary>
    public static ChangeStatusResult NotFound() =>
        new(ChangeStatusOutcome.NotFound, null, null);

    /// <summary>The move is not legal from the current status.</summary>
    /// <param name="reason">Why it was refused, naming what is allowed instead.</param>
    public static ChangeStatusResult Rejected(string reason) =>
        new(ChangeStatusOutcome.Rejected, null, reason);

    /// <summary>Something else wrote the work order between the read and the write.</summary>
    public static ChangeStatusResult ConcurrencyConflict() =>
        new(ChangeStatusOutcome.ConcurrencyConflict, null,
            "The work order was modified while this request was in flight. Re-read it and try again.");
}
