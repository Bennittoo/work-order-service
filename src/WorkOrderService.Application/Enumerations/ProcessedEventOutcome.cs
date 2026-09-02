namespace WorkOrderService.Application.Enumerations;

/// <summary>
/// What the background worker did with a progress event.
/// </summary>
/// <remarks>
/// Every member is a final decision: the event is recorded as handled and will never be processed
/// again, even where the outcome was a refusal. Transient failures deliberately have no member here,
/// because they roll back and stay retryable.
/// </remarks>
public enum ProcessedEventOutcome
{
    /// <summary>The work order moved to the status the event reported.</summary>
    Applied = 1,

    /// <summary>The work order already held that status, so nothing changed.</summary>
    NoOp = 2,

    /// <summary>The status change was not legal from the work order's current status.</summary>
    Rejected = 3,

    /// <summary>No work order exists with the external identifier the event named.</summary>
    WorkOrderNotFound = 4
}
