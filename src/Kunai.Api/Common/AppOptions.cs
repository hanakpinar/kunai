namespace Kunai.Api.Common;

public sealed class AppOptions
{
    public const string SectionName = "App";

    /// <summary>Base URL of the web app; links in emails point here.</summary>
    public Uri WebBaseUrl { get; set; } = new("http://localhost:3000");
}
