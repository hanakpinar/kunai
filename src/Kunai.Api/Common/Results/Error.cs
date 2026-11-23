namespace Kunai.Api.Common.Results;

/// <summary>An expected failure. Exceptions are reserved for bugs and infrastructure faults.</summary>
/// <param name="Code">Stable snake_case code that clients switch on and localize.</param>
/// <param name="Message">English, developer-facing explanation.</param>
/// <param name="Type">Category that decides the HTTP status code.</param>
public sealed record Error(string Code, string Message, ErrorType Type)
{
    /// <summary>Field-level details for validation errors: field name to error codes.</summary>
    public IReadOnlyDictionary<string, string[]>? Fields { get; init; }

    public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);

    public static Error ValidationField(string field, string code, string message) =>
        new(ErrorCodes.ValidationFailed, message, ErrorType.Validation)
        {
            Fields = new Dictionary<string, string[]>(StringComparer.Ordinal) { [field] = [code] },
        };

    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);

    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);

    public static Error BusinessRule(string code, string message) => new(code, message, ErrorType.BusinessRule);

    public static Error Unauthorized(string code, string message) => new(code, message, ErrorType.Unauthorized);

    public static Error Forbidden(string code, string message) => new(code, message, ErrorType.Forbidden);

    public static Error PreconditionFailed(string code, string message) => new(code, message, ErrorType.PreconditionFailed);

    public static Error Unavailable(string code, string message) => new(code, message, ErrorType.Unavailable);
}

public enum ErrorType
{
    Validation,
    NotFound,
    Conflict,
    BusinessRule,
    Unauthorized,
    Forbidden,
    PreconditionFailed,
    Unavailable,
}
