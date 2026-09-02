namespace WorkOrderService.Application.Enumerations;

/// <summary>What happened when a status change was attempted through the API.</summary>
public enum ChangeStatusOutcome
{
    /// <summary>
    /// The work order is now at the requested status. Also returned when it already held that
    /// status, because repeating a status is a success that writes nothing.
    /// </summary>
    Updated = 1,

    /// <summary>No work order exists with the supplied identifier.</summary>
    NotFound = 2,

    /// <summary>The move is not legal from the work order's current status.</summary>
    Rejected = 3,

    /// <summary>Something else wrote the work order between the read and the write.</summary>
    ConcurrencyConflict = 4
}
