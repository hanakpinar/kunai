namespace Kunai.Api.Common.Http;

public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public bool Enabled { get; set; } = true;

    public int AnonymousPerMinute { get; set; } = 60;

    public int AuthenticatedPerMinute { get; set; } = 300;

    public int AuthPerMinute { get; set; } = 10;

    public int RefreshPerMinute { get; set; } = 30;

    public int SearchPerMinute { get; set; } = 30;

    public int ImportPerMinute { get; set; } = 20;
}
