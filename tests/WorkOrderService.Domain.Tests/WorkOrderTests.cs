using WorkOrderService.Domain;

namespace WorkOrderService.Domain.Tests;

public class WorkOrderTests
{
    private static readonly DateTimeOffset CreatedOn = new(2026, 9, 1, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ChangedOn = new(2026, 9, 1, 9, 30, 0, TimeSpan.Zero);

    private static WorkOrder NewWorkOrder() =>
        WorkOrder.Create("EXT-001", "JHB-042", "Install equipment", CreatedOn);

    private static WorkOrder WorkOrderIn(WorkOrderStatus status)
    {
        var workOrder = NewWorkOrder();

        if (status == WorkOrderStatus.Pending)
        {
            return workOrder;
        }

        if (status is WorkOrderStatus.Completed or WorkOrderStatus.InProgress)
        {
            workOrder.ApplyStatus(WorkOrderStatus.InProgress, StatusChangeSource.Api, CreatedOn, CreatedOn);
        }

        if (status is WorkOrderStatus.Completed or WorkOrderStatus.Cancelled)
        {
            workOrder.ApplyStatus(status, StatusChangeSource.Api, CreatedOn, CreatedOn);
        }

        Assert.Equal(status, workOrder.Status);
        return workOrder;
    }

    [Fact]
    public void Create_starts_pending_and_records_a_creation_entry()
    {
        var workOrder = NewWorkOrder();

        Assert.Equal(WorkOrderStatus.Pending, workOrder.Status);
        Assert.Equal(CreatedOn, workOrder.CreatedAt);
        Assert.Equal(CreatedOn, workOrder.UpdatedAt);

        var entry = Assert.Single(workOrder.StatusHistory);
        Assert.Null(entry.FromStatus);
        Assert.Equal(WorkOrderStatus.Pending, entry.ToStatus);
        Assert.Equal(StatusChangeSource.Creation, entry.Source);
        Assert.Equal(CreatedOn, entry.OccurredAt);
        Assert.Equal(CreatedOn, entry.RecordedAt);
    }

    [Fact]
    public void Create_trims_whitespace_from_supplied_values()
    {
        var workOrder = WorkOrder.Create("  EXT-002  ", "  JHB-043  ", "  Survey  ", CreatedOn);

        Assert.Equal("EXT-002", workOrder.ExternalId);
        Assert.Equal("JHB-043", workOrder.SiteCode);
        Assert.Equal("Survey", workOrder.Description);
    }

    [Theory]
    [InlineData("", "JHB-042", "Install equipment")]
    [InlineData("   ", "JHB-042", "Install equipment")]
    [InlineData("EXT-001", "", "Install equipment")]
    [InlineData("EXT-001", "   ", "Install equipment")]
    [InlineData("EXT-001", "JHB-042", "")]
    [InlineData("EXT-001", "JHB-042", "   ")]
    public void Create_rejects_a_missing_required_value(string externalId, string siteCode, string description)
    {
        Assert.Throws<ArgumentException>(() => WorkOrder.Create(externalId, siteCode, description, CreatedOn));
    }

    [Fact]
    public void ApplyStatus_applies_a_legal_transition_and_appends_history()
    {
        var workOrder = NewWorkOrder();
        var eventId = Guid.NewGuid();
        var occurredAt = ChangedOn.AddMinutes(-15);

        var result = workOrder.ApplyStatus(
            WorkOrderStatus.InProgress,
            StatusChangeSource.ProgressEvent,
            occurredAt,
            ChangedOn,
            "Crew on site",
            eventId);

        Assert.Equal(StatusChangeOutcome.Applied, result.Outcome);
        Assert.Equal(WorkOrderStatus.InProgress, workOrder.Status);
        Assert.Equal(ChangedOn, workOrder.UpdatedAt);
        Assert.Equal(2, workOrder.StatusHistory.Count);

        var entry = workOrder.StatusHistory.Last();
        Assert.Equal(WorkOrderStatus.Pending, entry.FromStatus);
        Assert.Equal(WorkOrderStatus.InProgress, entry.ToStatus);
        Assert.Equal(occurredAt, entry.OccurredAt);
        Assert.Equal(ChangedOn, entry.RecordedAt);
        Assert.Equal(StatusChangeSource.ProgressEvent, entry.Source);
        Assert.Equal("Crew on site", entry.Details);
        Assert.Equal(eventId, entry.EventId);
    }

    /// <summary>
    /// An at-least-once event source will report the same status more than once. That is normal
    /// traffic, not a rule violation, so it succeeds without writing a duplicate history entry.
    /// </summary>
    [Fact]
    public void ApplyStatus_is_a_no_op_when_the_status_is_already_the_requested_one()
    {
        var workOrder = WorkOrderIn(WorkOrderStatus.InProgress);
        var historyBefore = workOrder.StatusHistory.Count;
        var updatedBefore = workOrder.UpdatedAt;

        var result = workOrder.ApplyStatus(
            WorkOrderStatus.InProgress, StatusChangeSource.ProgressEvent, ChangedOn, ChangedOn);

        Assert.Equal(StatusChangeOutcome.NoOp, result.Outcome);
        Assert.False(result.WasRejected);
        Assert.Equal(WorkOrderStatus.InProgress, workOrder.Status);
        Assert.Equal(historyBefore, workOrder.StatusHistory.Count);
        Assert.Equal(updatedBefore, workOrder.UpdatedAt);
    }

    [Fact]
    public void ApplyStatus_rejects_an_illegal_transition_and_leaves_the_work_order_untouched()
    {
        var workOrder = NewWorkOrder();

        var result = workOrder.ApplyStatus(
            WorkOrderStatus.Completed, StatusChangeSource.Api, ChangedOn, ChangedOn);

        Assert.Equal(StatusChangeOutcome.Rejected, result.Outcome);
        Assert.NotNull(result.Reason);
        Assert.Equal(WorkOrderStatus.Pending, workOrder.Status);
        Assert.Equal(CreatedOn, workOrder.UpdatedAt);
        Assert.Single(workOrder.StatusHistory);
    }

    [Theory]
    [InlineData(WorkOrderStatus.Completed, WorkOrderStatus.InProgress)]
    [InlineData(WorkOrderStatus.Completed, WorkOrderStatus.Cancelled)]
    [InlineData(WorkOrderStatus.Cancelled, WorkOrderStatus.InProgress)]
    [InlineData(WorkOrderStatus.Cancelled, WorkOrderStatus.Completed)]
    public void ApplyStatus_rejects_every_change_out_of_a_terminal_status(
        WorkOrderStatus terminal, WorkOrderStatus attempted)
    {
        var workOrder = WorkOrderIn(terminal);
        var historyBefore = workOrder.StatusHistory.Count;

        var result = workOrder.ApplyStatus(attempted, StatusChangeSource.Api, ChangedOn, ChangedOn);

        Assert.Equal(StatusChangeOutcome.Rejected, result.Outcome);
        Assert.Contains("terminal", result.Reason ?? string.Empty);
        Assert.Equal(terminal, workOrder.Status);
        Assert.Equal(historyBefore, workOrder.StatusHistory.Count);
    }

    [Fact]
    public void ApplyStatus_refuses_an_undefined_status()
    {
        var workOrder = NewWorkOrder();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            workOrder.ApplyStatus((WorkOrderStatus)99, StatusChangeSource.Api, ChangedOn, ChangedOn));
    }

    [Fact]
    public void ApplyStatus_refuses_to_impersonate_creation()
    {
        var workOrder = NewWorkOrder();

        Assert.Throws<ArgumentException>(() =>
            workOrder.ApplyStatus(WorkOrderStatus.InProgress, StatusChangeSource.Creation, ChangedOn, ChangedOn));
    }

    /// <summary>
    /// The invariant that justifies storing the current status alongside the history rather than
    /// deriving it: the two can never disagree, because one method writes both.
    /// </summary>
    [Fact]
    public void Status_always_matches_the_last_history_entry()
    {
        var workOrder = NewWorkOrder();
        Assert.Equal(workOrder.Status, workOrder.StatusHistory.Last().ToStatus);

        workOrder.ApplyStatus(WorkOrderStatus.InProgress, StatusChangeSource.Api, ChangedOn, ChangedOn);
        Assert.Equal(workOrder.Status, workOrder.StatusHistory.Last().ToStatus);

        workOrder.ApplyStatus(WorkOrderStatus.InProgress, StatusChangeSource.Api, ChangedOn, ChangedOn);
        Assert.Equal(workOrder.Status, workOrder.StatusHistory.Last().ToStatus);

        workOrder.ApplyStatus(WorkOrderStatus.Pending, StatusChangeSource.Api, ChangedOn, ChangedOn);
        Assert.Equal(workOrder.Status, workOrder.StatusHistory.Last().ToStatus);

        workOrder.ApplyStatus(WorkOrderStatus.Completed, StatusChangeSource.Api, ChangedOn, ChangedOn);
        Assert.Equal(workOrder.Status, workOrder.StatusHistory.Last().ToStatus);
    }
}
