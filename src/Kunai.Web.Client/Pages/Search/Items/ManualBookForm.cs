using System.ComponentModel.DataAnnotations;

using Kunai.Web.Client.Api.Models;

namespace Kunai.Web.Client.Pages.Search.Items;

/// <summary>A book added by hand, with the API's rules so most mistakes are caught before the request.</summary>
public sealed class ManualBookForm
{
    [Required(ErrorMessage = "Kitabın adını yaz.")]
    [StringLength(500, ErrorMessage = "Başlık en fazla 500 karakter olabilir.")]
    public string Title { get; set; } = string.Empty;

    /// <summary>Comma separated, as the user types them: "Orhan Pamuk, Elif Şafak".</summary>
    [StringLength(2000, ErrorMessage = "Yazar listesi çok uzun.")]
    public string? Authors { get; set; }

    [Range(1000, 3000, ErrorMessage = "Yıl 1000 ile 3000 arasında olmalı.")]
    public int? Year { get; set; }

    [RegularExpression("^97[89][0-9]{10}$", ErrorMessage = "ISBN-13, 978 ya da 979 ile başlayan 13 rakamdan oluşur.", MatchTimeoutInMilliseconds = 100)]
    public string? Isbn13 { get; set; }

    [Range(1, 100_000, ErrorMessage = "Sayfa sayısı 1 ile 100.000 arasında olmalı.")]
    public int? PageCount { get; set; }

    [StringLength(200, ErrorMessage = "Yayınevi en fazla 200 karakter olabilir.")]
    public string? Publisher { get; set; }

    [StringLength(5000, ErrorMessage = "Açıklama en fazla 5000 karakter olabilir.")]
    public string? Description { get; set; }

    /// <summary>The authors as the API wants them: a list, without blanks; null when none was given.</summary>
    private List<string>? AuthorList()
    {
        var authors = (Authors ?? string.Empty)
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        return authors.Count == 0 ? null : authors;
    }

    /// <summary>Drops the dashes and spaces people copy from book covers: 978-605-09-1234-5.</summary>
    public void NormalizeIsbn() =>
        Isbn13 = string.IsNullOrWhiteSpace(Isbn13) ? null : Isbn13.Replace("-", string.Empty, StringComparison.Ordinal).Replace(" ", string.Empty, StringComparison.Ordinal);

    public ManualBookRequest ToRequest() => new()
    {
        Title = Title.Trim(),
        Authors = AuthorList(),
        Year = Year,
        Isbn13 = string.IsNullOrWhiteSpace(Isbn13) ? null : Isbn13,
        PageCount = PageCount,
        Publisher = string.IsNullOrWhiteSpace(Publisher) ? null : Publisher.Trim(),
        Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
    };

    /// <summary>
    /// The id in a book_exists problem: the API names the existing book in <c>detail</c>
    /// ("... already in the catalog: {id}."), so the form can link to it.
    /// </summary>
    public static Guid? ExistingBookId(string? detail)
    {
        foreach (var word in (detail ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (Guid.TryParse(word.TrimEnd('.'), out var id))
            {
                return id;
            }
        }

        return null;
    }
}
