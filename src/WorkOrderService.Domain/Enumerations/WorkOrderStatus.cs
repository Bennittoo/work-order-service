namespace WorkOrderService.Domain.Enumerations;

/// <summary>
/// The lifecycle position of a work order.
/// </summary>
/// <remarks>
/// Numbered from 1 so that <c>default(WorkOrderStatus)</c> is not a valid status. An unset value
/// then fails validation instead of silently meaning <see cref="Pending"/>.
/// </remarks>
public enum WorkOrderStatus
{
    /// <summary>Created but not started. Every work order begins here.</summary>
    Pending = 1,

    /// <summary>Work has started on site.</summary>
    InProgress = 2,

    /// <summary>Work finished. Terminal: no further status change is accepted.</summary>
    Completed = 3,

    /// <summary>Work abandoned before completion. Terminal: no further status change is accepted.</summary>
    Cancelled = 4
}
