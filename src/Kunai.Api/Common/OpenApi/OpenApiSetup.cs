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
                    Description = "Track the books, movies, series, games, anime and manga you read, watch and play.",
                };
                return Task.CompletedTask;
            });
        });

        return services;
    }
}
