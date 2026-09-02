using WorkOrderService.Domain.Enumerations;

namespace WorkOrderService.Domain;

/// <summary>
/// One unit of work at one site, and the aggregate root for its status trail.
/// </summary>
/// <remarks>
/// The invariant this type protects is that status and history can never disagree. That is why
/// <see cref="ApplyStatus"/> is the only way to change <see cref="Status"/>, and why it writes the
/// history entry itself rather than leaving that to a caller.
/// </remarks>
public sealed class WorkOrder
{
    private readonly List<StatusHistoryEntry> _statusHistory = new();

    private WorkOrder()
    {
    }

    /// <summary>Identifier assigned by this service.</summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// The upstream system's own key for this work order. Unique, and how progress events address it,
    /// since an external system knows its own identifier rather than ours.
    /// </summary>
    public string ExternalId { get; private set; } = string.Empty;

    /// <summary>The site the work is being done at, for example <c>JHB-042</c>.</summary>
    public string SiteCode { get; private set; } = string.Empty;

    /// <summary>What the work is.</summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>Current lifecycle position. Always equal to the last history entry's <see cref="StatusHistoryEntry.ToStatus"/>.</summary>
    public WorkOrderStatus Status { get; private set; }

    /// <summary>When the work order was created.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>When the status last changed. Equal to <see cref="CreatedAt"/> until the first change.</summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>
    /// Optimistic concurrency token, configured in the persistence layer so this project stays free
    /// of EF Core. It guards against the API and the background worker writing the same row at once.
    /// </summary>
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    /// <summary>The full status trail, oldest first as written. Read-only: entries are appended by <see cref="ApplyStatus"/> alone.</summary>
    public IReadOnlyCollection<StatusHistoryEntry> StatusHistory => _statusHistory.AsReadOnly();

    /// <summary>
    /// Creates a work order in <see cref="WorkOrderStatus.Pending"/> with its first history entry.
    /// </summary>
    /// <param name="externalId">The upstream system's key. Required.</param>
    /// <param name="siteCode">The site the work is at. Required.</param>
    /// <param name="description">What the work is. Required.</param>
    /// <param name="now">The current time, supplied by the caller so the domain stays deterministic.</param>
    /// <exception cref="ArgumentException">A required value is missing or blank.</exception>
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
    /// The only way to change status. Appending the history entry lives here, alongside the
    /// assignment it describes, so that status and history cannot diverge.
    /// </summary>
    /// <param name="newStatus">The proposed status.</param>
    /// <param name="source">Which entry point is asking.</param>
    /// <param name="occurredAt">When the change happened according to its source.</param>
    /// <param name="now">When this service is recording it.</param>
    /// <param name="details">Optional free text to store against the change.</param>
    /// <param name="eventId">The progress event responsible, where there is one.</param>
    /// <returns>
    /// Applied when the status changed, NoOp when it already held the requested status, or Rejected
    /// when the move is not legal. None of the three throws.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">The status is not a defined value.</exception>
    /// <exception cref="ArgumentException">The source is Creation, which only Create may record.</exception>
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
