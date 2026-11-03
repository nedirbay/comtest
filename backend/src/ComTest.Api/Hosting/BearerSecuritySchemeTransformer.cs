using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace ComTest.Api.Hosting;

/// <summary>Describes bearer authentication in the OpenAPI document, for the API reference and clients.</summary>
internal sealed class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    private const string SchemeName = "Bearer";

    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[SchemeName] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
        };

        document.Security =
        [
            new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference(SchemeName, document)] = [] },
        ];

        return Task.CompletedTask;
    }
}
