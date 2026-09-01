namespace WorkOrderService.Domain;

/// <summary>
/// Numbered from 1 so that <c>default(WorkOrderStatus)</c> is not a valid status and an unset
/// value fails validation rather than silently meaning Pending.
/// </summary>
public enum WorkOrderStatus
{
    Pending = 1,
    InProgress = 2,
    Completed = 3,
    Cancelled = 4
}
