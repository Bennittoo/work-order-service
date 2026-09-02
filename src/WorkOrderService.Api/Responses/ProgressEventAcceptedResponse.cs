namespace WorkOrderService.Api.Responses;

/// <summary>
/// Acknowledgement that a progress event has been taken for processing.
/// </summary>
/// <remarks>
/// This confirms receipt, not validity. Whether the work order exists and whether the transition is
/// legal are decided by the background processor, and the result is observed by reading the work
/// order.
/// </remarks>
/// <param name="EventId">The identifier that was accepted, echoed back for correlation.</param>
/// <param name="Status">Always <c>Accepted</c>.</param>
public sealed record ProgressEventAcceptedResponse(Guid EventId, string Status);
