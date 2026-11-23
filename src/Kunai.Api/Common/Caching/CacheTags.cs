namespace Kunai.Api.Common.Caching;

/// <summary>HybridCache tags. Everything derived from one user's data carries that user's tag.</summary>
public static class CacheTags
{
    public static string User(Guid userId) => $"user:{userId}";
}
