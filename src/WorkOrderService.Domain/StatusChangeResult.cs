using WorkOrderService.Domain.Enumerations;

namespace WorkOrderService.Domain;

/// <summary>
/// The result of attempting a status change, returned instead of a boolean so that a no-op and a
/// rejection remain distinguishable to the caller.
/// </summary>
public sealed record StatusChangeResult
{
    private StatusChangeResult(StatusChangeOutcome outcome, string? reason)
    {
        Outcome = outcome;
        Reason = reason;
    }

    /// <summary>What happened.</summary>
    public StatusChangeOutcome Outcome { get; }

    /// <summary>Human readable explanation. Null when the change was applied.</summary>
    public string? Reason { get; }

    /// <summary>Whether the status actually changed.</summary>
    public bool WasApplied => Outcome == StatusChangeOutcome.Applied;

    /// <summary>Whether the change was refused as illegal.</summary>
    public bool WasRejected => Outcome == StatusChangeOutcome.Rejected;

    /// <summary>The status changed.</summary>
    public static StatusChangeResult Applied() => new(StatusChangeOutcome.Applied, null);

    /// <summary>The requested status was already held, so nothing changed.</summary>
    /// <param name="reason">Why nothing changed.</param>
    public static StatusChangeResult NoOp(string reason) => new(StatusChangeOutcome.NoOp, reason);

    /// <summary>The requested move is not legal from the current status.</summary>
    /// <param name="reason">Why the move was refused, naming what is allowed instead.</param>
    public static StatusChangeResult Rejected(string reason) => new(StatusChangeOutcome.Rejected, reason);
}
