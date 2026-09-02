namespace WorkOrderService.Domain.Enumerations;

/// <summary>
/// Who caused a status change. An audit trail that cannot say where a change came from only answers
/// half the question.
/// </summary>
public enum StatusChangeSource
{
    /// <summary>The work order was created. Only ever used on the first history entry.</summary>
    Creation = 1,

    /// <summary>A caller changed the status through the HTTP API.</summary>
    Api = 2,

    /// <summary>An external system reported the change through a progress event.</summary>
    ProgressEvent = 3
}
