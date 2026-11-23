using System.Text.RegularExpressions;

using Kunai.Api.Common.Results;

namespace Kunai.Api.Features.Identity;

/// <summary>
/// Usernames are public handles (/u/{username}): lowercase ASCII, 3-24 characters, starting with a letter.
/// ASCII-only avoids Turkish dotted/dotless i casing problems and look-alike characters.
/// </summary>
public static partial class UsernameRules
{
    public const string InvalidCode = "username_invalid";
    public const string ReservedCode = "username_reserved";
    public const string TakenCode = "username_taken";

    private static readonly HashSet<string> Reserved = new(StringComparer.Ordinal)
    {
        "admin", "administrator", "api", "app", "auth", "docs", "help", "kunai", "login", "logout", "me",
        "mod", "moderator", "null", "register", "root", "settings", "signup", "staff", "support", "system",
        "u", "undefined", "user", "users", "www",
    };

    /// <summary>Trims and lowercases with invariant rules; never culture-sensitive.</summary>
    public static string Normalize(string username) => username.Trim().ToLowerInvariant();

    /// <summary>Validates an already normalized username.</summary>
    public static Error? Validate(string normalized)
    {
        if (!Pattern().IsMatch(normalized))
        {
            return Error.ValidationField(
                "username",
                InvalidCode,
                "Usernames are 3-24 characters: lowercase letters, digits and underscores, starting with a letter.");
        }

        return Reserved.Contains(normalized)
            ? Error.ValidationField("username", ReservedCode, "This username is reserved.")
            : null;
    }

    [GeneratedRegex("^[a-z][a-z0-9_]{2,23}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex Pattern();
}
