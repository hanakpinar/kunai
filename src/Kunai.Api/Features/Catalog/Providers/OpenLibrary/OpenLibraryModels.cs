using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kunai.Api.Features.Catalog.Providers.OpenLibrary;

/// <summary>The parts of Open Library's JSON that Kunai reads (snake_case on the wire).</summary>
internal static class OpenLibraryModels
{
    /// <summary>The total comes as "num_found" and, still, under its older name "numFound".</summary>
    internal sealed record SearchResponse(
        int? NumFound,
        [property: JsonPropertyName("numFound")] int? NumFoundLegacy,
        IReadOnlyList<SearchDoc>? Docs);

    /// <summary>One work; the key looks like "/works/OL45883W".</summary>
    internal sealed record SearchDoc(
        string? Key,
        string? Title,
        IReadOnlyList<string>? AuthorName,
        int? FirstPublishYear,
        long? CoverI,
        int? NumberOfPagesMedian = null);

    /// <summary>
    /// The description is either a string or {"type": "/type/text", "value": "..."}; a cover id of -1 marks a
    /// removed cover; merged works have type "/type/redirect" and a location instead of data.
    /// </summary>
    internal sealed record Work(
        string? Title,
        JsonElement Description,
        IReadOnlyList<long>? Covers,
        IReadOnlyList<string>? Subjects,
        string? FirstPublishDate,
        IReadOnlyList<WorkAuthor>? Authors,
        JsonElement Type,
        string? Location);

    /// <summary>The author is usually {"key": "/authors/OL23919A"}.</summary>
    internal sealed record WorkAuthor(JsonElement Author);

    internal sealed record Author(string? Name, string? PersonalName);
}
