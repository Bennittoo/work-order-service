using Microsoft.AspNetCore.Http.HttpResults;
using WorkOrderService.Api.Contracts;
using WorkOrderService.Api.Processing;
using WorkOrderService.Api.Security;
using WorkOrderService.Api.Validation;

namespace WorkOrderService.Api.Endpoints;

public static class ProgressEventEndpoints
{
    private const int RetryAfterSeconds = 5;

    public static RouteGroupBuilder MapProgressEventEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/progress-events").WithTags("Progress events");

        group.MapPost("/", Accept)
            .RequireApiKey()
            .AddEndpointFilter<ValidationFilter<ProgressEventRequest>>()
            .WithName("SubmitProgressEvent");

        return group;
    }

    /// <summary>
    /// Accepts an event and returns immediately. Nothing here touches the database: whether the work
    /// order exists, and whether the transition is legal, are decided where the work is done. A 202
    /// means the event has been taken, not that it was valid, and the caller reads the outcome from
    /// the work order rather than from this response.
    /// </summary>
    private static Results<Accepted<ProgressEventAcceptedResponse>, ProblemHttpResult> Accept(
        ProgressEventRequest request,
        IProgressEventQueue queue,
        HttpContext httpContext,
        ILoggerFactory loggerFactory)
    {
        var message = new ProgressEventMessage(
            request.EventId,
            request.WorkOrderExternalId!.Trim(),
            request.NewStatus,
            request.OccurredAt,
            request.Details);

        if (!queue.TryEnqueue(message))
        {
            // Rejecting is the only honest option once the buffer is full. Dropping would lose an
            // event already acknowledged as accepted, and blocking would trade a bounded queue for
            // unbounded request latency. Telling the caller to retry is safe precisely because
            // processing is idempotent, so a resubmission of the same event identifier cannot
            // double-apply.
            loggerFactory
                .CreateLogger(typeof(ProgressEventEndpoints))
                .LogWarning(
                    "Progress event {EventId} rejected: queue at capacity {Capacity}.",
                    request.EventId,
                    queue.Capacity);

            httpContext.Response.Headers.RetryAfter = RetryAfterSeconds.ToString();

            return TypedResults.Problem(
                title: "Progress event queue is full",
                detail: "The service is behind on processing. Resubmit this event, unchanged, shortly.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        return TypedResults.Accepted(
            (string?)null,
            new ProgressEventAcceptedResponse(request.EventId, "Accepted"));
    }
}
