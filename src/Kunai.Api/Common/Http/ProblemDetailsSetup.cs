using System.Diagnostics;
using System.Text.Json;

using Kunai.Api.Common.Results;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Kunai.Api.Common.Http;

public static class ProblemDetailsSetup
{
    /// <summary>Every error response is application/problem+json with a stable "code" and a "traceId".</summary>
    public static IServiceCollection AddKunaiProblemDetails(this IServiceCollection services)
    {
        services.AddProblemDetails(options => options.CustomizeProblemDetails = Customize);
        services.AddExceptionHandler<GlobalExceptionHandler>();
        return services;
    }

    private static void Customize(ProblemDetailsContext context)
    {
        var problem = context.ProblemDetails;
        var status = problem.Status ?? context.HttpContext.Response.StatusCode;

        if (problem is HttpValidationProblemDetails validation)
        {
            CamelCaseErrorKeys(validation);
        }

        if (!problem.Extensions.ContainsKey("code"))
        {
            problem.Extensions["code"] = problem is HttpValidationProblemDetails
                ? ErrorCodes.ValidationFailed
                : ErrorCodes.ForStatus(status);
        }

        problem.Extensions["traceId"] = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
        problem.Instance ??= $"{context.HttpContext.Request.Method} {context.HttpContext.Request.Path}";
    }

    /// <summary>Built-in validation reports C# property names; clients see the camelCase JSON names.</summary>
    private static void CamelCaseErrorKeys(HttpValidationProblemDetails validation)
    {
        var renamed = validation.Errors.ToDictionary(
            pair => string.Join('.', pair.Key.Split('.').Select(JsonNamingPolicy.CamelCase.ConvertName)),
            pair => pair.Value,
            StringComparer.Ordinal);
        validation.Errors.Clear();
        foreach (var (key, value) in renamed)
        {
            validation.Errors[key] = value;
        }
    }
}

/// <summary>Turns unhandled exceptions into a 500 problem response without leaking details.</summary>
internal sealed partial class GlobalExceptionHandler(
    IProblemDetailsService problemDetails,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is BadHttpRequestException badRequest)
        {
            httpContext.Response.StatusCode = badRequest.StatusCode;
            return await problemDetails.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                Exception = exception,
                ProblemDetails = new ProblemDetails
                {
                    Status = badRequest.StatusCode,
                    Title = "The request could not be read.",
                    Extensions = { ["code"] = ErrorCodes.ForStatus(badRequest.StatusCode) },
                },
            });
        }

        LogUnhandled(logger, exception, httpContext.Request.Method, httpContext.Request.Path);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An unexpected error occurred.",
                Extensions = { ["code"] = ErrorCodes.InternalError },
            },
        });
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception for {Method} {Path}")]
    private static partial void LogUnhandled(ILogger logger, Exception exception, string method, PathString path);
}
