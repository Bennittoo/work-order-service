using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WorkOrderService.Application.Abstractions;
using WorkOrderService.Application.Enumerations;
using WorkOrderService.Application.Models;
using WorkOrderService.Application.Persistence;
using WorkOrderService.Domain;
using WorkOrderService.Domain.Enumerations;

namespace WorkOrderService.Application.Managers;

/// <summary>
/// The use cases of the service: creating, reading and changing work orders, and applying progress
/// events.
/// </summary>
/// <remarks>
/// This type owns the coordination. Loading, the transaction boundary, deduplication and recording
/// what happened all live here. What it does not own is the status rule itself: that stays on
/// <see cref="WorkOrder.ApplyStatus"/>, because the invariant being protected is that status and
/// history can never disagree, and only the entity can enforce that rather than rely on every caller
/// remembering to.
/// </remarks>
public sealed class WorkOrderServiceManager
{
    /// <summary>
    /// The fixed page size for listing, as the brief allows. Callers page with a page number and
    /// cannot ask for an unbounded result set.
    /// </summary>
    public const int PageSize = 25;

    private readonly WorkOrderDbContext _database;
    private readonly IUniqueConstraintDetector _uniqueConstraints;
    private readonly TimeProvider _clock;
    private readonly ILogger<WorkOrderServiceManager> _logger;

    /// <summary>Creates the manager.</summary>
    /// <param name="database">The unit of work.</param>
    /// <param name="uniqueConstraints">Recognises a unique key violation for the provider in use.</param>
    /// <param name="clock">Supplies the current time, so behaviour stays testable.</param>
    /// <param name="logger">Receives processing outcomes.</param>
    public WorkOrderServiceManager(
        WorkOrderDbContext database,
        IUniqueConstraintDetector uniqueConstraints,
        TimeProvider clock,
        ILogger<WorkOrderServiceManager> logger)
    {
        _database = database;
        _uniqueConstraints = uniqueConstraints;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>Creates a work order in Pending, with its creation history entry.</summary>
    /// <param name="externalId">The upstream system's key. Must not already be in use.</param>
    /// <param name="siteCode">The site the work is at.</param>
    /// <param name="description">What the work is.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    public async Task<CreateWorkOrderResult> CreateAsync(
        string externalId,
        string siteCode,
        string description,
        CancellationToken cancellationToken = default)
    {
        var now = _clock.GetUtcNow();
        var workOrder = WorkOrder.Create(externalId, siteCode, description, now);

        _database.WorkOrders.Add(workOrder);

        try
        {
            await _database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (_uniqueConstraints.IsUniqueViolation(exception))
        {
            // The unique index is the authority, not a prior existence check, which would race.
            return CreateWorkOrderResult.DuplicateExternalId(externalId.Trim());
        }

        return CreateWorkOrderResult.Created(WorkOrderModel.FromEntity(workOrder));
    }

    /// <summary>Reads one work order with its full status trail, or null if it does not exist.</summary>
    /// <param name="id">The work order identifier.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    public Task<WorkOrderModel?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        _database.WorkOrders
            .AsNoTracking()
            .Where(workOrder => workOrder.Id == id)
            .Select(workOrder => new WorkOrderModel(
                workOrder.Id,
                workOrder.ExternalId,
                workOrder.SiteCode,
                workOrder.Description,
                workOrder.Status,
                workOrder.CreatedAt,
                workOrder.UpdatedAt,
                workOrder.StatusHistory
                    .OrderBy(entry => entry.RecordedAt)
                    .Select(entry => new StatusHistoryEntryModel(
                        entry.FromStatus,
                        entry.ToStatus,
                        entry.OccurredAt,
                        entry.RecordedAt,
                        entry.Source,
                        entry.Details,
                        entry.EventId))
                    .ToList()))
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>Lists work orders newest first, optionally filtered by status.</summary>
    /// <param name="status">The status to filter by, or null for all.</param>
    /// <param name="page">The one-based page number.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    public async Task<PagedResult<WorkOrderSummaryModel>> ListAsync(
        WorkOrderStatus? status,
        int page,
        CancellationToken cancellationToken = default)
    {
        var query = _database.WorkOrders.AsNoTracking();

        if (status is { } wanted)
        {
            query = query.Where(workOrder => workOrder.Status == wanted);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            // Id is a tie-break, without which two work orders sharing a CreatedAt could appear on
            // two pages or on none.
            .OrderByDescending(workOrder => workOrder.CreatedAt)
            .ThenBy(workOrder => workOrder.Id)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .Select(workOrder => new WorkOrderSummaryModel(
                workOrder.Id,
                workOrder.ExternalId,
                workOrder.SiteCode,
                workOrder.Description,
                workOrder.Status,
                workOrder.CreatedAt,
                workOrder.UpdatedAt))
            .ToListAsync(cancellationToken);

        var totalPages = (int)Math.Ceiling(totalCount / (double)PageSize);

        return new PagedResult<WorkOrderSummaryModel>(items, page, PageSize, totalCount, totalPages);
    }

    /// <summary>Changes a work order status on behalf of an API caller.</summary>
    /// <param name="id">The work order identifier.</param>
    /// <param name="newStatus">The proposed status.</param>
    /// <param name="details">Optional free text to store against the change.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    public async Task<ChangeStatusResult> ChangeStatusAsync(
        Guid id,
        WorkOrderStatus newStatus,
        string? details,
        CancellationToken cancellationToken = default)
    {
        var workOrder = await _database.WorkOrders
            .Include(candidate => candidate.StatusHistory)
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (workOrder is null)
        {
            return ChangeStatusResult.NotFound();
        }

        var now = _clock.GetUtcNow();
        var result = workOrder.ApplyStatus(newStatus, StatusChangeSource.Api, now, now, details);

        if (result.WasRejected)
        {
            return ChangeStatusResult.Rejected(result.Reason!);
        }

        try
        {
            await _database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // The background worker changed this work order between the read and the write.
            return ChangeStatusResult.ConcurrencyConflict();
        }

        return ChangeStatusResult.Updated(WorkOrderModel.FromEntity(workOrder));
    }

    /// <summary>
    /// Applies one progress event, or returns null if that event has already been handled.
    /// </summary>
    /// <remarks>
    /// The work order change, its history entry and the record marking the event handled all go
    /// through a single <c>SaveChangesAsync</c>, so they share one transaction: an event is only ever
    /// marked processed if its effect actually committed.
    /// <para>
    /// A concurrency conflict is deliberately allowed to propagate, because re-reading and
    /// re-evaluating the transition against current state is the caller's decision to retry, not
    /// something to swallow here.
    /// </para>
    /// </remarks>
    /// <param name="message">The event to apply.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>What was done with the event, or null if it was a duplicate.</returns>
    public async Task<ProcessedEventOutcome?> ApplyProgressEventAsync(
        ProgressEventMessage message,
        CancellationToken cancellationToken = default)
    {
        // Redelivery is ordinary traffic from an at-least-once source, so the common duplicate is
        // recognised with a read rather than by letting an insert fail. This is an optimisation, not
        // the guard: it cannot close the window between the read and the write, which is why the
        // unique key still has to be there and still has to be caught.
        if (await _database.ProcessedEvents
                .AnyAsync(processed => processed.EventId == message.EventId, cancellationToken))
        {
            return null;
        }

        var workOrder = await _database.WorkOrders
            .Include(candidate => candidate.StatusHistory)
            .FirstOrDefaultAsync(
                candidate => candidate.ExternalId == message.WorkOrderExternalId, cancellationToken);

        var now = _clock.GetUtcNow();
        ProcessedEventOutcome outcome;
        string? detail;

        if (workOrder is null)
        {
            // Recorded rather than thrown away: without a row here the same unknown identifier would
            // be reprocessed on every redelivery, and there would be no trace that it ever arrived.
            outcome = ProcessedEventOutcome.WorkOrderNotFound;
            detail = $"No work order exists with external identifier '{message.WorkOrderExternalId}'.";
            _logger.LogWarning("Progress event names a work order that does not exist.");
        }
        else
        {
            var result = workOrder.ApplyStatus(
                message.NewStatus,
                StatusChangeSource.ProgressEvent,
                message.OccurredAt,
                now,
                message.Details,
                message.EventId);

            outcome = result.Outcome switch
            {
                StatusChangeOutcome.Applied => ProcessedEventOutcome.Applied,
                StatusChangeOutcome.NoOp => ProcessedEventOutcome.NoOp,
                _ => ProcessedEventOutcome.Rejected
            };
            detail = result.Reason;
        }

        _database.ProcessedEvents.Add(ProcessedEvent.Handled(
            message.EventId,
            message.WorkOrderExternalId,
            workOrder?.Id,
            outcome,
            detail,
            message.OccurredAt,
            now));

        try
        {
            await _database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (_uniqueConstraints.IsUniqueViolation(exception))
        {
            // Reached only when a redelivery slips past the existence check above, which it cannot
            // prevent because it is a read before a write. The unique key is the authority.
            return null;
        }

        return outcome;
    }
}
