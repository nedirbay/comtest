using System.Text.Json;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace ComTest.Api.Hosting;

/// <summary>
/// Completes enum schemas for generated clients. The generator leaves out the type, and one nullable
/// query parameter adds null to the schema that non-nullable request fields share. A nullable parameter
/// stays optional through its own required flag, so it does not need null here.
/// </summary>
internal sealed class StringEnumSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        var type = context.JsonTypeInfo.Type;

        if ((Nullable.GetUnderlyingType(type) ?? type).IsEnum && schema.Enum is not null)
        {
            schema.Type = JsonSchemaType.String;
            schema.Enum = [.. schema.Enum.Where(value => value?.GetValueKind() == JsonValueKind.String)];
        }

        return Task.CompletedTask;
    }
}
