using WorkOrderService.Domain;

namespace WorkOrderService.Domain.Tests;

public class StatusTransitionsTests
{
    [Theory]
    [InlineData(WorkOrderStatus.Pending, WorkOrderStatus.InProgress)]
    [InlineData(WorkOrderStatus.Pending, WorkOrderStatus.Cancelled)]
    [InlineData(WorkOrderStatus.InProgress, WorkOrderStatus.Completed)]
    [InlineData(WorkOrderStatus.InProgress, WorkOrderStatus.Cancelled)]
    public void Allows_the_transitions_that_make_up_the_lifecycle(WorkOrderStatus from, WorkOrderStatus to)
    {
        Assert.True(StatusTransitions.IsAllowed(from, to));
    }

    [Theory]
    [InlineData(WorkOrderStatus.Pending, WorkOrderStatus.Completed)]
    [InlineData(WorkOrderStatus.InProgress, WorkOrderStatus.Pending)]
    [InlineData(WorkOrderStatus.Completed, WorkOrderStatus.InProgress)]
    [InlineData(WorkOrderStatus.Completed, WorkOrderStatus.Cancelled)]
    [InlineData(WorkOrderStatus.Cancelled, WorkOrderStatus.InProgress)]
    [InlineData(WorkOrderStatus.Cancelled, WorkOrderStatus.Completed)]
    public void Blocks_transitions_that_skip_or_reverse_the_lifecycle(WorkOrderStatus from, WorkOrderStatus to)
    {
        Assert.False(StatusTransitions.IsAllowed(from, to));
    }

    [Theory]
    [InlineData(WorkOrderStatus.Completed)]
    [InlineData(WorkOrderStatus.Cancelled)]
    public void Treats_completed_and_cancelled_as_terminal(WorkOrderStatus status)
    {
        Assert.True(StatusTransitions.IsTerminal(status));
        Assert.Empty(StatusTransitions.AllowedFrom(status));
    }

    [Theory]
    [InlineData(WorkOrderStatus.Pending)]
    [InlineData(WorkOrderStatus.InProgress)]
    public void Treats_pending_and_in_progress_as_open(WorkOrderStatus status)
    {
        Assert.False(StatusTransitions.IsTerminal(status));
    }

    [Fact]
    public void Starts_the_lifecycle_at_pending()
    {
        Assert.Equal(WorkOrderStatus.Pending, StatusTransitions.Initial);
    }

    [Fact]
    public void Describes_every_status_so_no_status_is_silently_unreachable()
    {
        foreach (var status in Enum.GetValues<WorkOrderStatus>())
        {
            var exception = Record.Exception(() => StatusTransitions.AllowedFrom(status));
            Assert.Null(exception);
        }
    }
}
