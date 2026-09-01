using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using WorkOrderService.Api.Contracts;

namespace WorkOrderService.Api.Tests;

public static class ApiTestHelpers
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static string UniqueExternalId() => $"EXT-{Guid.NewGuid():N}"[..20];

    public static async Task<WorkOrderDetailResponse> CreateWorkOrderAsync(
        this HttpClient client, string externalId, string siteCode = "JHB-042")
    {
        var response = await client.PostAsJsonAsync(
            "/api/work-orders",
            new CreateWorkOrderRequest(externalId, siteCode, "Install equipment"),
            Json);

        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<WorkOrderDetailResponse>(Json))!;
    }

    public static async Task<WorkOrderDetailResponse> GetWorkOrderAsync(this HttpClient client, Guid id)
    {
        var response = await client.GetAsync($"/api/work-orders/{id}");
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<WorkOrderDetailResponse>(Json))!;
    }

    /// <summary>
    /// Polls until the condition holds. Event processing is asynchronous by design, so a test that
    /// reads immediately after a 202 is testing the race, not the behaviour.
    /// </summary>
    public static async Task<T> WaitUntilAsync<T>(
        Func<Task<T>> probe,
        Func<T, bool> condition,
        string description,
        TimeSpan? timeout = null)
    {
        var deadline = DateTimeOffset.UtcNow + (timeout ?? TimeSpan.FromSeconds(15));

        while (true)
        {
            var value = await probe();

            if (condition(value))
            {
                return value;
            }

            if (DateTimeOffset.UtcNow > deadline)
            {
                throw new TimeoutException($"Timed out waiting for: {description}");
            }

            await Task.Delay(25);
        }
    }
}
