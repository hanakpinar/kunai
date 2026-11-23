using Kunai.Api.Common.Results;

using Microsoft.AspNetCore.Identity;

namespace Kunai.Api.Features.Identity;

internal static class IdentityErrors
{
    public const string InvalidToken = "invalid_or_expired_token";

    /// <summary>Maps ASP.NET Core Identity errors to Kunai error codes.</summary>
    public static Error From(IEnumerable<IdentityError> errors)
    {
        var list = errors.ToList();
        if (list.Exists(e => e.Code is nameof(IdentityErrorDescriber.DuplicateUserName)))
        {
            return Error.Conflict(UsernameRules.TakenCode, "The username is already taken.");
        }

        if (list.Exists(e => e.Code is nameof(IdentityErrorDescriber.InvalidToken)))
        {
            return Error.Validation(InvalidToken, "The link is invalid or has expired.");
        }

        var fields = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var passwordCodes = list.Where(e => e.Code.StartsWith("Password", StringComparison.Ordinal) || string.Equals(e.Code, PwnedPasswordValidator.ErrorCode, StringComparison.Ordinal))
            .Select(e => e.Code switch
            {
                nameof(IdentityErrorDescriber.PasswordTooShort) => "password_too_short",
                PwnedPasswordValidator.ErrorCode => PwnedPasswordValidator.ErrorCode,
                _ => "password_rejected",
            })
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (passwordCodes.Length > 0)
        {
            fields["password"] = passwordCodes;
        }

        if (list.Exists(e => e.Code is nameof(IdentityErrorDescriber.InvalidUserName)))
        {
            fields["username"] = [UsernameRules.InvalidCode];
        }

        if (list.Exists(e => e.Code is nameof(IdentityErrorDescriber.InvalidEmail)))
        {
            fields["email"] = ["email_invalid"];
        }

        return fields.Count > 0
            ? new Error(ErrorCodes.ValidationFailed, "One or more fields are invalid.", ErrorType.Validation) { Fields = fields }
            : Error.Validation(ErrorCodes.BadRequest, string.Join(' ', list.Select(e => e.Description)));
    }
}
