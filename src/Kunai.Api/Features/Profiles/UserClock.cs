namespace Kunai.Api.Features.Profiles;

/// <summary>"Today" in a user's own time zone: a book finished at 23:30 on 31 December counts for that year.</summary>
public static class UserClock
{
    public static DateOnly Today(TimeProvider time, string timeZoneId)
    {
        ArgumentNullException.ThrowIfNull(time);
        var zone = TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out var found) ? found : TimeZoneInfo.Utc;
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(time.GetUtcNow(), zone).DateTime);
    }

    public static bool IsValidTimeZone(string? timeZoneId) =>
        !string.IsNullOrWhiteSpace(timeZoneId) && TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out _);
}
