using System.Net;
using System.Net.Http.Json;
using WorkOrderService.Api.Contracts;
using WorkOrderService.Domain;
using static WorkOrderService.Api.Tests.ApiTestHelpers;

namespace WorkOrderService.Api.Tests;

public sealed class ApiKeyTests : IClassFixture<WorkOrderApiFactory>
{
    private readonly WorkOrderApiFactory _factory;

    public ApiKeyTests(WorkOrderApiFactory factory) => _factory = factory;

    private HttpClient ClientWithoutKey()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Remove(WorkOrderApiFactory.ApiKeyHeader);
        return client;
    }

    [Fact]
    public async Task Creating_a_work_order_without_a_key_is_refused()
    {
        var response = await ClientWithoutKey().PostAsJsonAsync(
            "/api/work-orders",
            new CreateWorkOrderRequest(UniqueExternalId(), "JHB-042", "Install equipment"),
            Json);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Submitting_a_progress_event_without_a_key_is_refused()
    {
        var response = await ClientWithoutKey().PostAsJsonAsync(
            "/api/progress-events",
            new ProgressEventRequest(
                Guid.NewGuid(), "EXT-1", WorkOrderStatus.InProgress, DateTimeOffset.UtcNow, null),
            Json);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_wrong_key_is_refused()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Remove(WorkOrderApiFactory.ApiKeyHeader);
        client.DefaultRequestHeaders.Add(WorkOrderApiFactory.ApiKeyHeader, "not-the-key");

        var response = await client.PostAsJsonAsync(
            "/api/work-orders",
            new CreateWorkOrderRequest(UniqueExternalId(), "JHB-042", "Install equipment"),
            Json);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Reads stay open. The filter is applied per endpoint precisely so that this is a decision
    /// visible at the route definition rather than a side effect of middleware ordering.
    /// </summary>
    [Fact]
    public async Task Reading_does_not_require_a_key()
    {
        var response = await ClientWithoutKey().GetAsync("/api/work-orders");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task An_unauthenticated_write_is_refused_before_its_body_is_validated()
    {
        // The body is invalid on every field. A 401 rather than a 400 shows the key filter runs
        // first, so the service spends nothing validating a request it will not act on.
        var response = await ClientWithoutKey().PostAsJsonAsync(
            "/api/work-orders",
            new CreateWorkOrderRequest("", "", ""),
            Json);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
