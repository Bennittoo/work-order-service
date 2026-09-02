using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace WorkOrderService.Api.Swagger;

/// <summary>
/// The API serialises enums as strings, but Swashbuckle describes them as integers by default. A
/// document that disagrees with the wire format is actively misleading, so this brings the two back
/// into line for every enum rather than one at a time.
/// </summary>
public sealed class StringEnumSchemaFilter : ISchemaFilter
{
    /// <inheritdoc />
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        var type = Nullable.GetUnderlyingType(context.Type) ?? context.Type;

        if (!type.IsEnum)
        {
            return;
        }

        schema.Type = "string";
        schema.Format = null;
        schema.Enum = Enum.GetNames(type)
            .Select(name => (IOpenApiAny)new OpenApiString(name))
            .ToList();
    }
}
