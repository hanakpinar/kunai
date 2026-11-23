using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi;

namespace Kunai.Api.Common.OpenApi;

public static class OpenApiSetup
{
    public const string DocumentName = "v1";

    public static IServiceCollection AddKunaiOpenApi(this IServiceCollection services)
    {
        services.AddOpenApi(DocumentName, options =>
        {
            // Client generators fully support 3.1; pin it so an SDK upgrade cannot change the contract format.
            options.OpenApiVersion = OpenApiSpecVersion.OpenApi3_1;

            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info = new OpenApiInfo
                {
                    Title = "Kunai API",
                    Version = "v1",
                    Description = "Track the books, movies, series, games, anime and manga you read, watch and play. "
                        + "Errors are problem details with a stable \"code\"; see docs/errors.md.",
                };
                return Task.CompletedTask;
            });

            // Every problem response carries "code" and "traceId"; validation problems also "errors".
            options.AddSchemaTransformer((schema, context, _) =>
            {
                var type = context.JsonTypeInfo.Type;
                if (type != typeof(ProblemDetails) && type != typeof(HttpValidationProblemDetails))
                {
                    return Task.CompletedTask;
                }

                schema.Properties ??= new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal);
                schema.Properties["code"] = new OpenApiSchema { Type = JsonSchemaType.String, Description = "Stable snake_case error code to switch on and translate." };
                schema.Properties["traceId"] = new OpenApiSchema { Type = JsonSchemaType.String, Description = "Identifies the request in logs; include it in bug reports." };
                schema.Properties.TryAdd("errors", new OpenApiSchema
                {
                    Type = JsonSchemaType.Object,
                    Description = "Field name (camelCase) to error codes; present on validation problems.",
                    AdditionalProperties = new OpenApiSchema { Type = JsonSchemaType.Array, Items = new OpenApiSchema { Type = JsonSchemaType.String } },
                });
                schema.Required ??= new HashSet<string>(StringComparer.Ordinal);
                schema.Required.Add("code");
                return Task.CompletedTask;
            });

            // Rate limits apply to every operation.
            options.AddOperationTransformer((operation, context, _) =>
            {
                operation.Responses ??= [];
                operation.Responses.TryAdd("429", ProblemResponse("Too many requests; retry after the number of seconds in Retry-After.", context.Document));
                return Task.CompletedTask;
            });
        });

        return services;
    }

    /// <summary>A response described by the shared ProblemDetails schema.</summary>
    public static OpenApiResponse ProblemResponse(string description, OpenApiDocument? document) => new()
    {
        Description = description,
        Content = new Dictionary<string, OpenApiMediaType>(StringComparer.Ordinal)
        {
            ["application/problem+json"] = new() { Schema = new OpenApiSchemaReference(nameof(ProblemDetails), document) },
        },
    };
}
