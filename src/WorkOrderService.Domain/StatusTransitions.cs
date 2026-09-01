namespace WorkOrderService.Domain;

/// <summary>
/// The work order lifecycle expressed as data rather than control flow, so the allowed moves can
/// be enumerated for error messages and asserted directly in tests.
/// </summary>
public static class StatusTransitions
{
    private static readonly IReadOnlyDictionary<WorkOrderStatus, IReadOnlySet<WorkOrderStatus>> Map =
        new Dictionary<WorkOrderStatus, IReadOnlySet<WorkOrderStatus>>
        {
            [WorkOrderStatus.Pending] = new HashSet<WorkOrderStatus>
            {
                WorkOrderStatus.InProgress,
                WorkOrderStatus.Cancelled
            },
            [WorkOrderStatus.InProgress] = new HashSet<WorkOrderStatus>
            {
                WorkOrderStatus.Completed,
                WorkOrderStatus.Cancelled
            },
            [WorkOrderStatus.Completed] = new HashSet<WorkOrderStatus>(),
            [WorkOrderStatus.Cancelled] = new HashSet<WorkOrderStatus>()
        };

    public static WorkOrderStatus Initial => WorkOrderStatus.Pending;

    public static IReadOnlySet<WorkOrderStatus> AllowedFrom(WorkOrderStatus from) =>
        Map.TryGetValue(from, out var allowed)
            ? allowed
            : throw new ArgumentOutOfRangeException(nameof(from), from, "Unknown work order status.");

    public static bool IsAllowed(WorkOrderStatus from, WorkOrderStatus to) => AllowedFrom(from).Contains(to);

    public static bool IsTerminal(WorkOrderStatus status) => AllowedFrom(status).Count == 0;
}
