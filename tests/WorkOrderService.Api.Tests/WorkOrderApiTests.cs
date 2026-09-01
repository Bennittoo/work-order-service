using System.Net;
using System.Net.Http.Json;
using WorkOrderService.Api.Contracts;
using WorkOrderService.Domain;
using static WorkOrderService.Api.Tests.ApiTestHelpers;

namespace WorkOrderService.Api.Tests;

public sealed class WorkOrderApiTests : IClassFixture<WorkOrderApiFactory>
{
    private readonly HttpClient _client;

    public WorkOrderApiTests(WorkOrderApiFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Creating_a_work_order_returns_201_with_a_location_and_a_creation_entry()
    {
        var externalId = UniqueExternalId();

        var response = await _client.PostAsJsonAsync(
            "/api/work-orders",
            new CreateWorkOrderRequest(externalId, "JHB-042", "Install equipment"),
            Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = (await response.Content.ReadFromJsonAsync<WorkOrderDetailResponse>(Json))!;
        Assert.Equal($"/api/work-orders/{created.Id}", response.Headers.Location?.ToString());
        Assert.Equal(WorkOrderStatus.Pending, created.Status);

        var entry = Assert.Single(created.StatusHistory);
        Assert.Null(entry.FromStatus);
        Assert.Equal(WorkOrderStatus.Pending, entry.ToStatus);
        Assert.Equal(StatusChangeSource.Creation, entry.Source);
    }

    [Fact]
    public async Task Creating_a_work_order_twice_with_one_external_id_returns_409()
    {
        var externalId = UniqueExternalId();
        await _client.CreateWorkOrderAsync(externalId);

        var response = await _client.PostAsJsonAsync(
            "/api/work-orders",
            new CreateWorkOrderRequest(externalId, "JHB-042", "Install equipment"),
            Json);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Creating_a_work_order_without_required_values_returns_400_naming_each_field()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/work-orders",
            new CreateWorkOrderRequest("", "JHB-042", "   "),
            Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemBody>(Json);
        Assert.NotNull(problem);
        Assert.Contains(nameof(CreateWorkOrderRequest.ExternalId), problem!.Errors.Keys);
        Assert.Contains(nameof(CreateWorkOrderRequest.Description), problem.Errors.Keys);
        Assert.DoesNotContain(nameof(CreateWorkOrderRequest.SiteCode), problem.Errors.Keys);
    }

    [Fact]
    public async Task Getting_a_work_order_that_does_not_exist_returns_404()
    {
        var response = await _client.GetAsync($"/api/work-orders/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task An_illegal_transition_returns_409_and_leaves_the_work_order_untouched()
    {
        var created = await _client.CreateWorkOrderAsync(UniqueExternalId());

        var response = await _client.PutAsJsonAsync(
            $"/api/work-orders/{created.Id}/status",
            new UpdateWorkOrderStatusRequest(WorkOrderStatus.Completed, null),
            Json);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var after = await _client.GetWorkOrderAsync(created.Id);
        Assert.Equal(WorkOrderStatus.Pending, after.Status);
        Assert.Single(after.StatusHistory);
    }

    /// <summary>
    /// The rule the whole deduplication design leans on: repeating a status is success, and success
    /// that writes nothing. If this became a rejection, ordinary at-least-once traffic would be
    /// treated as an error.
    /// </summary>
    [Fact]
    public async Task Setting_the_status_to_the_one_it_already_holds_succeeds_without_adding_history()
    {
        var created = await _client.CreateWorkOrderAsync(UniqueExternalId());

        await _client.PutAsJsonAsync(
            $"/api/work-orders/{created.Id}/status",
            new UpdateWorkOrderStatusRequest(WorkOrderStatus.InProgress, null),
            Json);

        var afterFirst = await _client.GetWorkOrderAsync(created.Id);
        Assert.Equal(2, afterFirst.StatusHistory.Count);

        var repeat = await _client.PutAsJsonAsync(
            $"/api/work-orders/{created.Id}/status",
            new UpdateWorkOrderStatusRequest(WorkOrderStatus.InProgress, null),
            Json);

        Assert.Equal(HttpStatusCode.OK, repeat.StatusCode);

        var afterRepeat = await _client.GetWorkOrderAsync(created.Id);
        Assert.Equal(WorkOrderStatus.InProgress, afterRepeat.Status);
        Assert.Equal(2, afterRepeat.StatusHistory.Count);
    }

    [Fact]
    public async Task History_comes_back_in_the_order_it_happened()
    {
        var created = await _client.CreateWorkOrderAsync(UniqueExternalId());

        await _client.PutAsJsonAsync(
            $"/api/work-orders/{created.Id}/status",
            new UpdateWorkOrderStatusRequest(WorkOrderStatus.InProgress, "Started"),
            Json);
        await _client.PutAsJsonAsync(
            $"/api/work-orders/{created.Id}/status",
            new UpdateWorkOrderStatusRequest(WorkOrderStatus.Completed, "Finished"),
            Json);

        var detail = await _client.GetWorkOrderAsync(created.Id);

        Assert.Equal(
            new WorkOrderStatus[] { WorkOrderStatus.Pending, WorkOrderStatus.InProgress, WorkOrderStatus.Completed },
            detail.StatusHistory.Select(h => h.ToStatus).ToArray());
    }

    [Fact]
    public async Task Listing_filters_by_status_and_reports_the_fixed_page_size()
    {
        var externalId = UniqueExternalId();
        var created = await _client.CreateWorkOrderAsync(externalId, "CPT-777");

        await _client.PutAsJsonAsync(
            $"/api/work-orders/{created.Id}/status",
            new UpdateWorkOrderStatusRequest(WorkOrderStatus.Cancelled, "Site withdrawn"),
            Json);

        var page = await _client.GetFromJsonAsync<PagedResponse<WorkOrderSummaryResponse>>(
            "/api/work-orders?status=cancelled", Json);

        Assert.NotNull(page);
        Assert.Contains(page!.Items, w => w.ExternalId == externalId);
        Assert.All(page.Items, w => Assert.Equal(WorkOrderStatus.Cancelled, w.Status));
        Assert.Equal(1, page.Page);
        Assert.Equal(25, page.PageSize);
    }

    [Theory]
    [InlineData("/api/work-orders?status=Nonsense")]
    [InlineData("/api/work-orders?page=0")]
    public async Task Listing_rejects_input_it_cannot_honour(string url)
    {
        var response = await _client.GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private sealed record ValidationProblemBody(Dictionary<string, string[]> Errors);
}
