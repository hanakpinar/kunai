using System.Globalization;

namespace Kunai.Web.Client.Pages.Profile;

/// <summary>
/// Turkish display formatting for the profile pages. Month names and separators are fixed here instead of
/// coming from CultureInfo("tr-TR"), because the WebAssembly runtime may not ship Turkish culture data.
/// </summary>
public static class ProfileFormat
{
    private static readonly string[] Months =
        ["Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran", "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık"];

    private static readonly NumberFormatInfo Turkish = new()
    {
        NumberDecimalSeparator = ",",
        NumberGroupSeparator = ".",
    };

    /// <summary>Ocak ... Aralık for 1 ... 12.</summary>
    public static string MonthName(int month) => Months[Math.Clamp(month, 1, 12) - 1];

    /// <summary>The first letter of each month, for chart labels: O, Ş, M, N, ...</summary>
    public static IReadOnlyList<string> MonthInitials { get; } = [.. Months.Select(m => m[..1])];

    /// <summary>15 Mart 2025, in the viewer's local time.</summary>
    public static string Date(DateTimeOffset? value)
    {
        if (value is not { } instant)
        {
            return string.Empty;
        }

        var local = instant.ToLocalTime();
        return string.Create(CultureInfo.InvariantCulture, $"{local.Day} {MonthName(local.Month)} {local.Year}");
    }

    /// <summary>12.450</summary>
    public static string Number(int? value) => (value ?? 0).ToString("#,0", Turkish);

    /// <summary>8,4</summary>
    public static string Rating(double value) => value.ToString("0.0", Turkish);

    /// <summary>A 0-100 share of <paramref name="part"/> in <paramref name="whole"/>.</summary>
    public static int Percent(int? part, int? whole) =>
        whole is > 0 ? Math.Clamp((int)((long)(part ?? 0) * 100 / whole.Value), 0, 100) : 0;

    /// <summary>A CSS width such as "40%", always with invariant digits.</summary>
    public static string Width(int percent) => string.Create(CultureInfo.InvariantCulture, $"width: {Math.Clamp(percent, 0, 100)}%");

    /// <summary>A CSS height such as "40%", always with invariant digits.</summary>
    public static string Height(int percent) => string.Create(CultureInfo.InvariantCulture, $"height: {Math.Clamp(percent, 0, 100)}%");
}
