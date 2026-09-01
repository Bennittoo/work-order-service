namespace WorkOrderService.Domain;

public sealed class WorkOrder
{
    private readonly List<StatusHistoryEntry> _statusHistory = new();

    private WorkOrder()
    {
    }

    public Guid Id { get; private set; }

    /// <summary>
    /// The upstream system's own key for this work order. Unique, and how progress events address it,
    /// since an external system knows its own identifier rather than ours.
    /// </summary>
    public string ExternalId { get; private set; } = string.Empty;

    public string SiteCode { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public WorkOrderStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>
    /// Optimistic concurrency token, configured in the persistence layer so the domain stays free of
    /// EF Core. It guards against the API and the background worker writing the same row at once.
    /// </summary>
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public IReadOnlyCollection<StatusHistoryEntry> StatusHistory => _statusHistory.AsReadOnly();

    public static WorkOrder Create(string externalId, string siteCode, string description, DateTimeOffset now)
    {
        var workOrder = new WorkOrder
        {
            Id = Guid.NewGuid(),
            ExternalId = Required(externalId, nameof(externalId)),
            SiteCode = Required(siteCode, nameof(siteCode)),
            Description = Required(description, nameof(description)),
            Status = StatusTransitions.Initial,
            CreatedAt = now,
            UpdatedAt = now
        };

        workOrder._statusHistory.Add(StatusHistoryEntry.ForCreation(workOrder.Id, now));

        return workOrder;
    }

    /// <summary>
    /// The only way to change status. Appending the history entry lives here, alongside the assignment
    /// it describes, so that status and history cannot diverge.
    /// </summary>
    public StatusChangeResult ApplyStatus(
        WorkOrderStatus newStatus,
        StatusChangeSource source,
        DateTimeOffset occurredAt,
        DateTimeOffset now,
        string? details = null,
        Guid? eventId = null)
    {
        if (!Enum.IsDefined(newStatus))
        {
            throw new ArgumentOutOfRangeException(nameof(newStatus), newStatus, "Unknown work order status.");
        }

        if (source == StatusChangeSource.Creation)
        {
            throw new ArgumentException("Creation is recorded by Create, not by ApplyStatus.", nameof(source));
        }

        if (newStatus == Status)
        {
            return StatusChangeResult.NoOp($"Work order is already {Status}.");
        }

        if (!StatusTransitions.IsAllowed(Status, newStatus))
        {
            return StatusChangeResult.Rejected(DescribeRejection(Status, newStatus));
        }

        var fromStatus = Status;
        Status = newStatus;
        UpdatedAt = now;
        _statusHistory.Add(StatusHistoryEntry.ForChange(
            Id, fromStatus, newStatus, occurredAt, now, source, details, eventId));

        return StatusChangeResult.Applied();
    }

    private static string DescribeRejection(WorkOrderStatus from, WorkOrderStatus to)
    {
        if (StatusTransitions.IsTerminal(from))
        {
            return $"Work order is {from}, which is terminal and accepts no further status changes.";
        }

        var allowed = string.Join(", ", StatusTransitions.AllowedFrom(from));
        return $"Cannot move from {from} to {to}. Allowed from {from}: {allowed}.";
    }

    private static string Required(string value, string parameterName) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Value is required.", parameterName)
            : value.Trim();
}
