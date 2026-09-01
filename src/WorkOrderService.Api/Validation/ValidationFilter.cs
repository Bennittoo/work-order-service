namespace WorkOrderService.Api.Validation;

public sealed class ValidationFilter<TRequest> : IEndpointFilter
    where TRequest : IValidatableRequest
{
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
