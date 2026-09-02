using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using WorkOrderService.Api.Security;

namespace WorkOrderService.Api.Swagger;

/// <summary>
/// Attaches the key requirement to the operations that actually enforce it. A document-level
/// requirement would be simpler, but it would also claim the open read endpoints need a key.
/// </summary>
public sealed class ApiKeySecurityOperationFilter : IOperationFilter
{
    /// <summary>The identifier of the security scheme this filter references.</summary>
    public const string SchemeId = "ApiKey";

    /// <inheritdoc />
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var requiresKey = context.ApiDescription.ActionDescriptor.EndpointMetadata
            .OfType<ApiKeyRequirementMetadata>()
            .Any();

        if (!requiresKey)
        {
            return;
        }

        operation.Security =
        [
            new OpenApiSecurityRequirement
            {
                [new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference { Id = SchemeId, Type = ReferenceType.SecurityScheme }
                }] = Array.Empty<string>()
            }
        ];
    }
}
