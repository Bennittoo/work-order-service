namespace WorkOrderService.Domain;

public enum StatusChangeOutcome
{
    /// <summary>The status changed and a history entry was appended.</summary>
    Applied = 1,

    /// <summary>
    /// The work order already held the requested status. Nothing changed, and this is not an error:
    /// an at-least-once event source will legitimately report the same status more than once.
    /// </summary>
    NoOp = 2,

    /// <summary>The move is not legal from the current status. Nothing changed.</summary>
    Rejected = 3
}
