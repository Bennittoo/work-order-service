using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace WorkOrderService.Api.Security;

/// <summary>
/// Guards the write endpoints. Applied per endpoint rather than as middleware so that reads stay
/// open and the protected surface is visible at the route definition.
/// </summary>
public sealed class ApiKeyEndpointFilter : IEndpointFilter
{
    private readonly ApiKeyOptions _options;

    public ApiKeyEndpointFilter(IOptions<ApiKeyOptions> options) => _options = options.Value;

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var supplied = context.HttpContext.Request.Headers[_options.HeaderName].ToString();

        if (!Matches(supplied, _options.Value))
        {
            return TypedResults.Problem(
                title: "Missing or invalid API key",
                detail: $"Write requests require a valid {_options.HeaderName} header.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        return await next(context);
    }

    /// <summary>
    /// Fixed-time comparison. An ordinary string comparison returns as soon as two bytes differ,
    /// which leaks how much of a guess was correct.
    /// </summary>
    private static bool Matches(string supplied, string? expected)
    {
        if (string.IsNullOrEmpty(supplied) || string.IsNullOrEmpty(expected))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(supplied),
            Encoding.UTF8.GetBytes(expected));
    }
}
