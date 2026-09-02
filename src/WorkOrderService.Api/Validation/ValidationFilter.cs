using WorkOrderService.Application.Validations;

namespace WorkOrderService.Api.Validation;

/// <summary>
/// Runs a request's own rules before the handler, and turns failures into a validation problem
/// response.
/// </summary>
/// <remarks>
/// The filter is the adapter, not the rules. Minimal APIs do not validate request bodies the way MVC
/// model binding does, so this closes that gap; what counts as valid lives in the application layer,
/// which has no dependency on ASP.NET Core.
/// </remarks>
/// <typeparam name="TRequest">The request type being validated.</typeparam>
public sealed class ValidationFilter<TRequest> : IEndpointFilter
    where TRequest : IValidatableRequest
{
    /// <inheritdoc />
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        if (context.Arguments.OfType<TRequest>().FirstOrDefault() is not { } request)
        {
            return TypedResults.Problem(
                title: "Malformed request",
                detail: "A request body of the expected shape is required.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var errors = request.Validate();

        return errors.Count > 0
            ? TypedResults.ValidationProblem(errors)
            : await next(context);
    }
}
