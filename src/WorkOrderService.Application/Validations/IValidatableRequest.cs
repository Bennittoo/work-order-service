namespace WorkOrderService.Application.Validations;

/// <summary>
/// Implemented by an inbound request so it can state its own rules.
/// </summary>
/// <remarks>
/// Minimal APIs do not validate request bodies the way MVC model binding does, so the gap has to be
/// closed deliberately. The rules themselves live in this project; the API layer supplies the
/// endpoint filter that runs them and turns failures into a problem response.
/// </remarks>
public interface IValidatableRequest
{
    /// <summary>Field name to messages, in the shape a validation problem response expects. Empty when valid.</summary>
    IDictionary<string, string[]> Validate();
}
