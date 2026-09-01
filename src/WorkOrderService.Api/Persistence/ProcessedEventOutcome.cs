namespace WorkOrderService.Api.Persistence;

/// <summary>
/// What the background worker did with an event. Every one of these is a final decision: the event
/// is recorded as handled and will never be processed again, even where the outcome was a refusal.
/// Transient failures deliberately have no member here, because they roll back and stay retryable.
/// </summary>
public enum ProcessedEventOutcome
{
    Applied = 1,
    NoOp = 2,
    Rejected = 3,
    WorkOrderNotFound = 4
}
