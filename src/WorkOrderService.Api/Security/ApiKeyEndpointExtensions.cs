using Microsoft.AspNetCore.Mvc;

namespace WorkOrderService.Api.Security;

/// <summary>Marker metadata, so the OpenAPI document can tell which operations are protected.</summary>
public sealed class ApiKeyRequirementMetadata;

public static class ApiKeyEndpointExtensions
{
    /// <summary>
    /// Enforces the key, documents the 401, and marks the operation for the OpenAPI security filter.
    /// One call so the three cannot drift apart: an endpoint that enforces a key but is documented as
    /// open is a worse contract than one with no document at all.
    /// </summary>
    public static RouteHandlerBuilder RequireApiKey(this RouteHandlerBuilder builder) =>
        builder
            .AddEndpointFilter<ApiKeyEndpointFilter>()
            .WithMetadata(new ApiKeyRequirementMetadata())
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized);
}
