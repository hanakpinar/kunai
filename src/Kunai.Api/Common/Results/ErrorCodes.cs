namespace Kunai.Api.Common.Results;

/// <summary>Error codes shared across features. Feature-specific codes live next to their feature.</summary>
public static class ErrorCodes
{
    public const string BadRequest = "bad_request";
    public const string ValidationFailed = "validation_failed";
    public const string Unauthorized = "unauthorized";
    public const string Forbidden = "forbidden";
    public const string NotFound = "not_found";
    public const string MethodNotAllowed = "method_not_allowed";
    public const string Conflict = "conflict";
    public const string PreconditionFailed = "precondition_failed";
    public const string UnsupportedMediaType = "unsupported_media_type";
    public const string BusinessRule = "business_rule_violated";
    public const string RateLimited = "rate_limited";
    public const string InternalError = "internal_error";
    public const string ServiceUnavailable = "service_unavailable";
    public const string CsrfHeaderMissing = "csrf_header_missing";
    public const string InvalidCursor = "invalid_cursor";

    /// <summary>The default code for a status code when the producer did not set one.</summary>
    public static string ForStatus(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => BadRequest,
        StatusCodes.Status401Unauthorized => Unauthorized,
        StatusCodes.Status403Forbidden => Forbidden,
        StatusCodes.Status404NotFound => NotFound,
        StatusCodes.Status405MethodNotAllowed => MethodNotAllowed,
        StatusCodes.Status409Conflict => Conflict,
        StatusCodes.Status412PreconditionFailed => PreconditionFailed,
        StatusCodes.Status415UnsupportedMediaType => UnsupportedMediaType,
        StatusCodes.Status422UnprocessableEntity => BusinessRule,
        StatusCodes.Status429TooManyRequests => RateLimited,
        StatusCodes.Status503ServiceUnavailable => ServiceUnavailable,
        >= 500 => InternalError,
        _ => BadRequest,
    };
}
