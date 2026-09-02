using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using WorkOrderService.Api.Requests;
using WorkOrderService.Api.Security;
using WorkOrderService.Api.Validation;
using WorkOrderService.Application.Enumerations;
using WorkOrderService.Application.Managers;
using WorkOrderService.Application.Models;
using WorkOrderService.Application.Validations;

namespace WorkOrderService.Api.Endpoints;

/// <summary>The work order routes.</summary>
public static class WorkOrderEndpoints
{
    /// <summary>Maps the work order routes onto the application.</summary>
    /// <param name="app">The route builder to map onto.</param>
    public static RouteGroupBuilder MapWorkOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/work-orders").WithTags("Work orders");

        // RequireApiKey is registered before validation so an unauthenticated caller is turned away
        // before the service spends anything validating a body it is not going to act on.
        group.MapPost("/", CreateAsync)
            .RequireApiKey()
            .AddEndpointFilter<ValidationFilter<CreateWorkOrderRequest>>()
            .WithName("CreateWorkOrder");

        group.MapGet("/{id:guid}", GetByIdAsync)
            .WithName("GetWorkOrderById");

        group.MapGet("/", ListAsync)
            .WithName("ListWorkOrders");

        // PUT rather than PATCH: the target is the status sub-resource, and the request replaces it
        // outright. That also makes the call idempotent, which matters because the same status may
        // legitimately be submitted twice.
        group.MapPut("/{id:guid}/status", UpdateStatusAsync)
            .RequireApiKey()
            .AddEndpointFilter<ValidationFilter<UpdateWorkOrderStatusRequest>>()
            .WithName("UpdateWorkOrderStatus");

        return group;
    }

    private static async Task<Results<Created<WorkOrderModel>, Conflict<ProblemDetails>>> CreateAsync(
        CreateWorkOrderRequest request,
        WorkOrderServiceManager manager,
        CancellationToken cancellationToken)
    {
        var result = await manager.CreateAsync(
            request.ExternalId!, request.SiteCode!, request.Description!, cancellationToken);

        if (result.Outcome == CreateWorkOrderOutcome.DuplicateExternalId)
        {
            return TypedResults.Conflict(new ProblemDetails
            {
                Title = "Duplicate external identifier",
                Detail = result.Detail,
                Status = StatusCodes.Status409Conflict
            });
        }

        var created = result.WorkOrder!;
        return TypedResults.Created($"/api/work-orders/{created.Id}", created);
    }

    private static async Task<Results<Ok<WorkOrderModel>, NotFound>> GetByIdAsync(
        Guid id,
        WorkOrderServiceManager manager,
        CancellationToken cancellationToken)
    {
        var workOrder = await manager.GetAsync(id, cancellationToken);

        return workOrder is null ? TypedResults.NotFound() : TypedResults.Ok(workOrder);
    }

    private static async Task<Results<Ok<PagedResult<WorkOrderSummaryModel>>, ValidationProblem>> ListAsync(
        WorkOrderServiceManager manager,
        CancellationToken cancellationToken,
        string? status = null,
        int page = 1)
    {
        var validation = WorkOrderValidator.ValidateListQuery(status, page);

        if (validation.Errors.Count > 0)
        {
            return TypedResults.ValidationProblem(validation.Errors);
        }

        var results = await manager.ListAsync(validation.Status, validation.Page, cancellationToken);

        return TypedResults.Ok(results);
    }

    private static async Task<Results<Ok<WorkOrderModel>, NotFound, Conflict<ProblemDetails>>> UpdateStatusAsync(
        Guid id,
        UpdateWorkOrderStatusRequest request,
        WorkOrderServiceManager manager,
        CancellationToken cancellationToken)
    {
        var result = await manager.ChangeStatusAsync(id, request.Status, request.Details, cancellationToken);

        return result.Outcome switch
        {
            ChangeStatusOutcome.Updated => TypedResults.Ok(result.WorkOrder!),
            ChangeStatusOutcome.NotFound => TypedResults.NotFound(),
            ChangeStatusOutcome.Rejected => TypedResults.Conflict(new ProblemDetails
            {
                Title = "Status change not allowed",
                Detail = result.Detail,
                Status = StatusCodes.Status409Conflict
            }),
            _ => TypedResults.Conflict(new ProblemDetails
            {
                Title = "Work order changed concurrently",
                Detail = result.Detail,
                Status = StatusCodes.Status409Conflict
            })
        };
    }
}
