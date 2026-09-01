namespace WorkOrderService.Api.Validation;

/// <summary>
/// Minimal APIs do not validate request bodies the way MVC model binding does, so a request type
/// states its own rules and <see cref="ValidationFilter{TRequest}"/> enforces them before the
/// handler runs.
/// </summary>
public interface IValidatableRequest
{
    /// <summary>Field name to messages, in the shape <c>ValidationProblem</c> expects. Empty when valid.</summary>
    IDictionary<string, string[]> Validate();
}
