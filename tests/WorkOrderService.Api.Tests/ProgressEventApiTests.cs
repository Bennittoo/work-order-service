using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WorkOrderService.Api.Contracts;
using WorkOrderService.Api.Persistence;
using WorkOrderService.Api.Processing;
using WorkOrderService.Domain;
using static WorkOrderService.Api.Tests.ApiTestHelpers;

namespace WorkOrderService.Api.Tests;

public sealed class ProgressEventApiTests : IClassFixture<WorkOrderApiFactory>
{
    private static readonly DateTimeOffset OccurredAt = new(2026, 9, 1, 6, 30, 0, TimeSpan.Zero);

    private readonly WorkOrderApiFactory _factory;
    private readonly HttpClient _client;

    public ProgressEventApiTests(WorkOrderApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task An_accepted_event_updates_the_work_order_in_the_background()
    {
        var externalId = UniqueExternalId();
        var created = await _client.CreateWorkOrderAsync(externalId);

        var response = await SubmitAsync(Guid.NewGuid(), externalId, WorkOrderStatus.InProgress, "Crew on site");
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var detail = await WaitUntilAsync(
            () => _client.GetWorkOrderAsync(created.Id),
            w => w.Status == WorkOrderStatus.InProgress,
            "the work order to reach InProgress");

        var applied = detail.StatusHistory.Last();
        Assert.Equal(StatusChangeSource.ProgressEvent, applied.Source);
        Assert.Equal("Crew on site", applied.Details);

        // The event reported one time and we recorded it at another. Conflating the two would make a
        // late-arriving event indistinguishable from a late-processed one.
        Assert.Equal(OccurredAt, applied.OccurredAt);
        Assert.NotEqual(applied.OccurredAt, applied.RecordedAt);
    }

    /// <summary>
    /// The requirement is that resubmitting an event identifier produces no further effects, which
    /// includes not appending a second history entry. Asserting only on the status would pass even
    /// if the history were duplicated.
    /// </summary>
    [Fact]
    public async Task Resubmitting_one_event_identifier_changes_the_work_order_exactly_once()
    {
        var externalId = UniqueExternalId();
        var created = await _client.CreateWorkOrderAsync(externalId);
        var eventId = Guid.NewGuid();

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var response = await SubmitAsync(eventId, externalId, WorkOrderStatus.InProgress, "Crew on site");
            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        }

        var detail = await WaitUntilAsync(
            () => _client.GetWorkOrderAsync(created.Id),
            w => w.Status == WorkOrderStatus.InProgress,
            "the work order to reach InProgress");

        Assert.Equal(2, detail.StatusHistory.Count);
        Assert.Single(detail.StatusHistory, h => h.EventId == eventId);

        await using var db = _factory.CreateDbContext();
        Assert.Equal(1, await db.ProcessedEvents.CountAsync(e => e.EventId == eventId));
    }

    [Fact]
    public async Task An_event_for_an_unknown_work_order_is_recorded_rather_than_retried_forever()
    {
        var eventId = Guid.NewGuid();

        await SubmitAsync(eventId, "NO-SUCH-WORK-ORDER", WorkOrderStatus.Completed, null);

        var processed = await WaitForProcessedEventAsync(eventId);

        Assert.Equal(ProcessedEventOutcome.WorkOrderNotFound, processed.Outcome);
        Assert.Null(processed.WorkOrderId);
        Assert.Contains("NO-SUCH-WORK-ORDER", processed.Detail);
    }

    [Fact]
    public async Task An_event_proposing_an_illegal_transition_is_recorded_as_rejected()
    {
        var externalId = UniqueExternalId();
        var created = await _client.CreateWorkOrderAsync(externalId);
        var eventId = Guid.NewGuid();

        await SubmitAsync(eventId, externalId, WorkOrderStatus.Completed, null);

        var processed = await WaitForProcessedEventAsync(eventId);

        Assert.Equal(ProcessedEventOutcome.Rejected, processed.Outcome);
        Assert.Contains("Cannot move from Pending to Completed", processed.Detail);

        var detail = await _client.GetWorkOrderAsync(created.Id);
        Assert.Equal(WorkOrderStatus.Pending, detail.Status);
        Assert.Single(detail.StatusHistory);
    }

    [Fact]
    public async Task An_event_repeating_the_current_status_is_recorded_as_a_no_op()
    {
        var externalId = UniqueExternalId();
        var created = await _client.CreateWorkOrderAsync(externalId);

        await SubmitAsync(Guid.NewGuid(), externalId, WorkOrderStatus.InProgress, null);
        await WaitUntilAsync(
            () => _client.GetWorkOrderAsync(created.Id),
            w => w.Status == WorkOrderStatus.InProgress,
            "the work order to reach InProgress");

        var repeatEventId = Guid.NewGuid();
        await SubmitAsync(repeatEventId, externalId, WorkOrderStatus.InProgress, null);

        var processed = await WaitForProcessedEventAsync(repeatEventId);

        Assert.Equal(ProcessedEventOutcome.NoOp, processed.Outcome);

        var detail = await _client.GetWorkOrderAsync(created.Id);
        Assert.Equal(2, detail.StatusHistory.Count);
    }

    [Fact]
    public async Task An_event_without_an_identifier_is_refused_at_the_endpoint()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/progress-events",
            new ProgressEventRequest(Guid.Empty, "EXT-1", WorkOrderStatus.InProgress, OccurredAt, null),
            Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Shutdown must not discard events that were already answered with a 202. The processor is
    /// stopped directly, which is the same path the host takes on shutdown, so this exercises the
    /// drain rather than approximating it.
    /// </summary>
    [Fact]
    public async Task Events_accepted_before_shutdown_are_drained_rather_than_dropped()
    {
        using var factory = new WorkOrderApiFactory();
        var client = factory.CreateClient();

        var externalId = UniqueExternalId();
        await client.CreateWorkOrderAsync(externalId);

        var queue = factory.Services.GetRequiredService<IProgressEventQueue>();
        // A List rather than an array: with an array the compiler binds Contains to the
        // ReadOnlySpan overload, which EF cannot translate.
        var eventIds = Enumerable.Range(0, 50).Select(_ => Guid.NewGuid()).ToList();

        foreach (var eventId in eventIds)
        {
            var queued = queue.TryEnqueue(new ProgressEventMessage(
                eventId, externalId, WorkOrderStatus.InProgress, OccurredAt, null));

            Assert.True(queued, "the queue should have room for this batch");
        }

        var processor = factory.Services.GetServices<IHostedService>().OfType<ProgressEventProcessor>().Single();
        await processor.StopAsync(CancellationToken.None);

        await using var db = factory.CreateDbContext();
        var processedCount = await db.ProcessedEvents.CountAsync(e => eventIds.Contains(e.EventId));

        Assert.Equal(eventIds.Count, processedCount);
    }

    private Task<HttpResponseMessage> SubmitAsync(
        Guid eventId, string externalId, WorkOrderStatus newStatus, string? details) =>
        _client.PostAsJsonAsync(
            "/api/progress-events",
            new ProgressEventRequest(eventId, externalId, newStatus, OccurredAt, details),
            Json);

    private async Task<ProcessedEvent> WaitForProcessedEventAsync(Guid eventId) =>
        await WaitUntilAsync(
            async () =>
            {
                await using var db = _factory.CreateDbContext();
                return await db.ProcessedEvents.AsNoTracking()
                    .FirstOrDefaultAsync(e => e.EventId == eventId);
            },
            processed => processed is not null,
            $"event {eventId} to be processed")
        ?? throw new InvalidOperationException("Unreachable: the wait only returns a non-null value.");
}
