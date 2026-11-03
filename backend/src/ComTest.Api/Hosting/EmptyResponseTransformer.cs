using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace ComTest.Api.Hosting;

/// <summary>
/// Removes the media type that [Produces] on the base controller adds to responses without a body.
/// Otherwise a generated client would try to parse JSON from an empty 202 or 204.
/// </summary>
internal sealed class EmptyResponseTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        foreach (var response in operation.Responses?.Values.AsEnumerable() ?? [])
        {
            if (response.Content?.Values.All(mediaType => mediaType.Schema is null) == true)
            {
                response.Content.Clear();
            }
        }

        return Task.CompletedTask;
    }
}
