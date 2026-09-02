using WorkOrderService.Domain.Enumerations;

namespace WorkOrderService.Domain;

/// <summary>
/// The work order lifecycle expressed as data rather than control flow, so the allowed moves can be
/// enumerated for error messages and asserted directly in tests.
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

    /// <summary>The status every new work order starts in.</summary>
    public static WorkOrderStatus Initial => WorkOrderStatus.Pending;

    /// <summary>Every status reachable in one step from <paramref name="from"/>.</summary>
    /// <param name="from">The status to move away from.</param>
    /// <exception cref="ArgumentOutOfRangeException">The status is not part of the lifecycle.</exception>
    public static IReadOnlySet<WorkOrderStatus> AllowedFrom(WorkOrderStatus from) =>
        Map.TryGetValue(from, out var allowed)
            ? allowed
            : throw new ArgumentOutOfRangeException(nameof(from), from, "Unknown work order status.");

    /// <summary>Whether moving directly from one status to another is legal.</summary>
    /// <param name="from">The current status.</param>
    /// <param name="to">The proposed status.</param>
    public static bool IsAllowed(WorkOrderStatus from, WorkOrderStatus to) => AllowedFrom(from).Contains(to);

    /// <summary>Whether a status accepts no further changes.</summary>
    /// <param name="status">The status to test.</param>
    public static bool IsTerminal(WorkOrderStatus status) => AllowedFrom(status).Count == 0;
}
