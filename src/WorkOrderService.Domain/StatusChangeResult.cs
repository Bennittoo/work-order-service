namespace WorkOrderService.Domain;

public sealed record StatusChangeResult
{
    private StatusChangeResult(StatusChangeOutcome outcome, string? reason)
    {
        Outcome = outcome;
        Reason = reason;
    }

    public StatusChangeOutcome Outcome { get; }

    /// <summary>Human readable explanation. Null when the change was applied.</summary>
    public string? Reason { get; }

    public bool WasApplied => Outcome == StatusChangeOutcome.Applied;

    public bool WasRejected => Outcome == StatusChangeOutcome.Rejected;

    public static StatusChangeResult Applied() => new(StatusChangeOutcome.Applied, null);

    public static StatusChangeResult NoOp(string reason) => new(StatusChangeOutcome.NoOp, reason);

    public static StatusChangeResult Rejected(string reason) => new(StatusChangeOutcome.Rejected, reason);
}
