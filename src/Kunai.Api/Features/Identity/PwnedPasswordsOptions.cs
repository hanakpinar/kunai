namespace Kunai.Api.Features.Identity;

public sealed class PwnedPasswordsOptions
{
    public const string SectionName = "Security:PwnedPasswords";

    /// <summary>Rejects passwords found in known breaches (Have I Been Pwned, k-anonymity range API).</summary>
    public bool Enabled { get; set; } = true;
}
