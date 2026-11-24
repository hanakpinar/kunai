using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

using Kunai.Api.IntegrationTests.Infrastructure;

namespace Kunai.Api.IntegrationTests.Contract;

/// <summary>
/// The OpenAPI document is the contract the web app and the mobile apps generate their clients from.
/// It is committed as openapi/kunai.v1.json; any change to it shows up in code review as a diff.
/// After an intended change, run the tests once with KUNAI_UPDATE_OPENAPI=1 and regenerate the web client.
/// </summary>
public sealed class OpenApiContractTests(KunaiApiFactory factory)
{
    private static readonly JsonSerializerOptions Pretty = new()
    {
        WriteIndented = true,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    [Fact]
    public async Task OpenApi_document_matches_the_committed_contract()
    {
        var json = await factory.CreateApiClient().GetStringAsync(new Uri("/openapi/v1.json", UriKind.Relative), TestContext.Current.CancellationToken);
        var document = JsonNode.Parse(json)!.AsObject();
        document.Remove("servers");
        var current = document.ToJsonString(Pretty) + "\n";

        var path = Path.Combine(RepositoryRoot(), "openapi", "kunai.v1.json");
        if (string.Equals(Environment.GetEnvironmentVariable("KUNAI_UPDATE_OPENAPI"), "1", StringComparison.Ordinal))
        {
            await File.WriteAllTextAsync(path, current, TestContext.Current.CancellationToken);
            return;
        }

        File.Exists(path).Should().BeTrue("the contract file is missing; run the tests with KUNAI_UPDATE_OPENAPI=1 to create it");
        var committed = (await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken)).ReplaceLineEndings("\n");
        committed.Should().Be(current, "the API contract changed: review it, then run the tests with KUNAI_UPDATE_OPENAPI=1 and regenerate the web client (scripts/generate-web-client.sh)");
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Kunai.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Kunai.slnx not found above the test output folder.");
    }
}
