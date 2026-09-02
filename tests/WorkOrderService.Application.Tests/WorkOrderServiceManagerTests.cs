using Microsoft.EntityFrameworkCore;
using WorkOrderService.Application.Enumerations;
using WorkOrderService.Application.Models;
using WorkOrderService.Domain.Enumerations;

namespace WorkOrderService.Application.Tests;

public sealed class WorkOrderServiceManagerTests : IDisposable
{
    private static readonly DateTimeOffset EventOccurredAt = new(2026, 9, 2, 6, 30, 0, TimeSpan.Zero);

    private readonly ManagerTestHost _host = new();

    public void Dispose() => _host.Dispose();

    [Fact]
    public async Task Creating_a_work_order_starts_it_pending_with_a_creation_entry()
    {
        // Arrange
        var manager = _host.NewManager();

        // Act
        var result = await manager.CreateAsync("EXT-1", "JHB-042", "Install equipment");

        // Assert
        Assert.Equal(CreateWorkOrderOutcome.Created, result.Outcome);
        var created = Assert.IsType<WorkOrderModel>(result.WorkOrder);
        Assert.Equal(WorkOrderStatus.Pending, created.Status);

        var entry = Assert.Single(created.StatusHistory);
        Assert.Null(entry.FromStatus);
        Assert.Equal(StatusChangeSource.Creation, entry.Source);
    }

    [Fact]
    public async Task Creating_a_second_work_order_with_the_same_external_id_is_refused()
    {
        // Arrange
        await _host.NewManager().CreateAsync("EXT-DUP", "JHB-042", "Install equipment");

        // Act
        var result = await _host.NewManager().CreateAsync("EXT-DUP", "CPT-007", "Something else");

        // Assert
        Assert.Equal(CreateWorkOrderOutcome.DuplicateExternalId, result.Outcome);
        Assert.Null(result.WorkOrder);
        Assert.Contains("EXT-DUP", result.Detail);
    }

    [Fact]
    public async Task Changing_status_to_an_illegal_value_is_rejected_and_writes_nothing()
    {
        // Arrange
        var created = await CreateAsync("EXT-ILLEGAL");

        // Act
        var result = await _host.NewManager()
            .ChangeStatusAsync(created.Id, WorkOrderStatus.Completed, details: null);

        // Assert
        Assert.Equal(ChangeStatusOutcome.Rejected, result.Outcome);
        Assert.Contains("Cannot move from Pending to Completed", result.Detail);

        var reloaded = await _host.NewManager().GetAsync(created.Id);
        Assert.Equal(WorkOrderStatus.Pending, reloaded!.Status);
        Assert.Single(reloaded.StatusHistory);
    }

    [Fact]
    public async Task Changing_status_to_the_one_already_held_succeeds_without_adding_history()
    {
        // Arrange
        var created = await CreateAsync("EXT-NOOP");
        await _host.NewManager().ChangeStatusAsync(created.Id, WorkOrderStatus.InProgress, null);

        // Act
        var result = await _host.NewManager()
            .ChangeStatusAsync(created.Id, WorkOrderStatus.InProgress, null);

        // Assert
        Assert.Equal(ChangeStatusOutcome.Updated, result.Outcome);
        Assert.Equal(2, result.WorkOrder!.StatusHistory.Count);
    }

    [Fact]
    public async Task Changing_status_on_a_work_order_that_does_not_exist_reports_not_found()
    {
        // Arrange
        var manager = _host.NewManager();

        // Act
        var result = await manager.ChangeStatusAsync(Guid.NewGuid(), WorkOrderStatus.InProgress, null);

        // Assert
        Assert.Equal(ChangeStatusOutcome.NotFound, result.Outcome);
    }

    [Fact]
    public async Task A_progress_event_moves_the_work_order_and_records_the_event()
    {
        // Arrange
        var created = await CreateAsync("EXT-EVENT");
        var eventId = Guid.NewGuid();
        var message = new ProgressEventMessage(
            eventId, "EXT-EVENT", WorkOrderStatus.InProgress, EventOccurredAt, "Crew on site");

        // Act
        var outcome = await _host.NewManager().ApplyProgressEventAsync(message);

        // Assert
        Assert.Equal(ProcessedEventOutcome.Applied, outcome);

        var reloaded = await _host.NewManager().GetAsync(created.Id);
        var applied = reloaded!.StatusHistory.Last();
        Assert.Equal(StatusChangeSource.ProgressEvent, applied.Source);
        Assert.Equal(eventId, applied.EventId);

        // The event reported one time and we recorded it at another. Conflating the two would make a
        // late-arriving event indistinguishable from a late-processed one.
        Assert.Equal(EventOccurredAt, applied.OccurredAt);
        Assert.NotEqual(applied.OccurredAt, applied.RecordedAt);
    }

    /// <summary>
    /// The requirement is that resubmitting an event identifier produces no further effects, which
    /// includes not appending a second history entry.
    /// </summary>
    [Fact]
    public async Task Applying_the_same_event_twice_changes_the_work_order_once()
    {
        // Arrange
        var created = await CreateAsync("EXT-IDEMPOTENT");
        var message = new ProgressEventMessage(
            Guid.NewGuid(), "EXT-IDEMPOTENT", WorkOrderStatus.InProgress, EventOccurredAt, null);

        // Act
        var first = await _host.NewManager().ApplyProgressEventAsync(message);
        var second = await _host.NewManager().ApplyProgressEventAsync(message);

        // Assert
        Assert.Equal(ProcessedEventOutcome.Applied, first);
        Assert.Null(second);

        var reloaded = await _host.NewManager().GetAsync(created.Id);
        Assert.Equal(2, reloaded!.StatusHistory.Count);

        await using var context = _host.NewContext();
        Assert.Equal(1, await context.ProcessedEvents.CountAsync(e => e.EventId == message.EventId));
    }

    [Fact]
    public async Task An_event_for_an_unknown_work_order_is_recorded_rather_than_retried_forever()
    {
        // Arrange
        var message = new ProgressEventMessage(
            Guid.NewGuid(), "NO-SUCH-WORK-ORDER", WorkOrderStatus.Completed, EventOccurredAt, null);

        // Act
        var outcome = await _host.NewManager().ApplyProgressEventAsync(message);

        // Assert
        Assert.Equal(ProcessedEventOutcome.WorkOrderNotFound, outcome);

        await using var context = _host.NewContext();
        var recorded = await context.ProcessedEvents.SingleAsync(e => e.EventId == message.EventId);
        Assert.Null(recorded.WorkOrderId);
        Assert.Contains("NO-SUCH-WORK-ORDER", recorded.Detail);
    }

    [Fact]
    public async Task An_event_proposing_an_illegal_transition_is_recorded_as_rejected()
    {
        // Arrange
        await CreateAsync("EXT-REJECT");
        var message = new ProgressEventMessage(
            Guid.NewGuid(), "EXT-REJECT", WorkOrderStatus.Completed, EventOccurredAt, null);

        // Act
        var outcome = await _host.NewManager().ApplyProgressEventAsync(message);

        // Assert
        Assert.Equal(ProcessedEventOutcome.Rejected, outcome);
    }

    [Fact]
    public async Task Listing_filters_by_status_and_reports_the_fixed_page_size()
    {
        // Arrange
        var cancelled = await CreateAsync("EXT-LIST-1");
        await CreateAsync("EXT-LIST-2");
        await _host.NewManager().ChangeStatusAsync(cancelled.Id, WorkOrderStatus.Cancelled, "Withdrawn");

        // Act
        var page = await _host.NewManager().ListAsync(WorkOrderStatus.Cancelled, page: 1);

        // Assert
        Assert.Equal(1, page.TotalCount);
        Assert.Equal(25, page.PageSize);
        Assert.All(page.Items, item => Assert.Equal(WorkOrderStatus.Cancelled, item.Status));
    }

    private async Task<WorkOrderModel> CreateAsync(string externalId)
    {
        var result = await _host.NewManager().CreateAsync(externalId, "JHB-042", "Install equipment");
        return result.WorkOrder!;
    }
}
