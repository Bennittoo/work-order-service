namespace WorkOrderService.Api.Security;

/// <summary>Settings for the API key that guards the write endpoints.</summary>
public sealed class ApiKeyOptions
{
    /// <summary>The configuration section these settings bind from.</summary>
    public const string SectionName = "ApiKey";

    /// <summary>The request header carrying the key.</summary>
    public string HeaderName { get; set; } = "X-Api-Key";

    /// <summary>
    /// The expected key. Required, and supplied through user secrets or environment configuration
    /// rather than a committed file. Startup validation refuses to run without it, because a missing
    /// key silently disabling the check is worse than the service failing to start.
    /// </summary>
    public string? Value { get; set; }
}
