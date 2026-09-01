namespace WorkOrderService.Domain;

/// <summary>
/// Who caused a status change. An audit trail that cannot say where a change came from only
/// answers half the question.
/// </summary>
public enum StatusChangeSource
{
    Creation = 1,
    Api = 2,
    ProgressEvent = 3
}
