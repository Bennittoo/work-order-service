using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorkOrderService.Api.Contracts;
using WorkOrderService.Api.Persistence;
using WorkOrderService.Api.Security;
using WorkOrderService.Api.Validation;
using WorkOrderService.Domain;

namespace WorkOrderService.Api.Endpoints;

public static class WorkOrderEndpoints
{
    /// <summary>
    /// Fixed, as the brief allows. Callers page with <c>?page=</c> and cannot ask for an unbounded
    /// result set.
    /// </summary>
    public const int PageSize = 25;

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

    private static async Task<Results<Created<WorkOrderDetailResponse>, Conflict<ProblemDetails>>> CreateAsync(
        CreateWorkOrderRequest request,
        WorkOrderDbContext db,
        TimeProvider clock,
        IUniqueConstraintDetector uniqueConstraints,
        CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var workOrder = WorkOrder.Create(request.ExternalId!, request.SiteCode!, request.Description!, now);

        db.WorkOrders.Add(workOrder);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (uniqueConstraints.IsUniqueViolation(exception))
        {
            // The unique index is the authority, not a prior existence check, which would race.
            return TypedResults.Conflict(new ProblemDetails
            {
                Title = "Duplicate external identifier",
                Detail = $"A work order with external identifier '{request.ExternalId}' already exists.",
                Status = StatusCodes.Status409Conflict
            });
        }

        return TypedResults.Created($"/api/work-orders/{workOrder.Id}", ToDetail(workOrder));
    }

    private static async Task<Results<Ok<WorkOrderDetailResponse>, NotFound>> GetByIdAsync(
        Guid id,
        WorkOrderDbContext db,
        CancellationToken cancellationToken)
    {
        var response = await db.WorkOrders
            .AsNoTracking()
            .Where(w => w.Id == id)
            .Select(w => new WorkOrderDetailResponse(
                w.Id,
                w.ExternalId,
                w.SiteCode,
                w.Description,
                w.Status,
                w.CreatedAt,
                w.UpdatedAt,
                w.StatusHistory
                    .OrderBy(h => h.RecordedAt)
                    .Select(h => new StatusHistoryEntryResponse(
                        h.FromStatus, h.ToStatus, h.OccurredAt, h.RecordedAt, h.Source, h.Details, h.EventId))
                    .ToList()))
            .FirstOrDefaultAsync(cancellationToken);

        return response is null ? TypedResults.NotFound() : TypedResults.Ok(response);
    }

    private static async Task<Results<Ok<PagedResponse<WorkOrderSummaryResponse>>, ValidationProblem>> ListAsync(
        WorkOrderDbContext db,
        CancellationToken cancellationToken,
        string? status = null,
        int page = 1)
    {
        var errors = new Dictionary<string, string[]>();

        WorkOrderStatus? filter = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (Enum.TryParse<WorkOrderStatus>(status, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
            {
                filter = parsed;
            }
            else
            {
                errors[nameof(status)] =
                    [$"Unknown status. Expected one of: {string.Join(", ", Enum.GetNames<WorkOrderStatus>())}."];
            }
        }

        if (page < 1)
        {
            errors[nameof(page)] = ["Must be 1 or greater."];
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var query = db.WorkOrders.AsNoTracking();
        if (filter is { } wanted)
        {
            query = query.Where(w => w.Status == wanted);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            // Id is a tie-break, without which two work orders sharing a CreatedAt could appear on
            // two pages or on none.
            .OrderByDescending(w => w.CreatedAt)
            .ThenBy(w => w.Id)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .Select(w => new WorkOrderSummaryResponse(
                w.Id, w.ExternalId, w.SiteCode, w.Description, w.Status, w.CreatedAt, w.UpdatedAt))
            .ToListAsync(cancellationToken);

        var totalPages = (int)Math.Ceiling(totalCount / (double)PageSize);

        return TypedResults.Ok(new PagedResponse<WorkOrderSummaryResponse>(
            items, page, PageSize, totalCount, totalPages));
    }

    private static async Task<Results<Ok<WorkOrderDetailResponse>, NotFound, Conflict<ProblemDetails>>> UpdateStatusAsync(
        Guid id,
        UpdateWorkOrderStatusRequest request,
        WorkOrderDbContext db,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var workOrder = await db.WorkOrders
            .Include(w => w.StatusHistory)
            .FirstOrDefaultAsync(w => w.Id == id, cancellationToken);

        if (workOrder is null)
        {
            return TypedResults.NotFound();
        }

        var now = clock.GetUtcNow();
        var result = workOrder.ApplyStatus(request.Status, StatusChangeSource.Api, now, now, request.Details);

        if (result.WasRejected)
        {
            return TypedResults.Conflict(new ProblemDetails
            {
                Title = "Status change not allowed",
                Detail = result.Reason,
                Status = StatusCodes.Status409Conflict
            });
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // The background worker changed this work order between the read and the write.
            return TypedResults.Conflict(new ProblemDetails
            {
                Title = "Work order changed concurrently",
                Detail = "The work order was modified while this request was in flight. Re-read it and try again.",
                Status = StatusCodes.Status409Conflict
            });
        }

        return TypedResults.Ok(ToDetail(workOrder));
    }

    private static WorkOrderDetailResponse ToDetail(WorkOrder workOrder) =>
        new(workOrder.Id,
            workOrder.ExternalId,
            workOrder.SiteCode,
            workOrder.Description,
            workOrder.Status,
            workOrder.CreatedAt,
            workOrder.UpdatedAt,
            workOrder.StatusHistory
                .OrderBy(h => h.RecordedAt)
                .Select(h => new StatusHistoryEntryResponse(
                    h.FromStatus, h.ToStatus, h.OccurredAt, h.RecordedAt, h.Source, h.Details, h.EventId))
                .ToList());
}
