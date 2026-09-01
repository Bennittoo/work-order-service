namespace WorkOrderService.Api.Security;

public sealed class ApiKeyOptions
{
    public const string SectionName = "ApiKey";

    public string HeaderName { get; set; } = "X-Api-Key";

    /// <summary>
    /// Required. Startup validation refuses to run without it, because a missing key silently
    /// disabling the check is worse than the service failing to start.
    /// </summary>
    public string? Value { get; set; }
}
