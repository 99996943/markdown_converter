using System.Globalization;
using System.Text.Json;
using LegalAgent.Chunking.Model;
using LegalAgent.Chunking.Serialization;
using LegalAgent.Chunking.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace LegalAgent.Chunking.Tests.Corpus;

/// <summary>
/// T044 (FR-271): golden chunk files of five corpus documents — a one-column regulation with versions, a
/// table-document, a tariff with a multi-page table, a procedure and a legal act. Metadata comes from
/// corpus/manifest.json. <c>UPDATE_GOLDEN=1</c> rewrites the files; changing them needs the owner's approval.
/// </summary>
public sealed class GoldenChunksTests
{
    // As the corpus generator does: values that reach the model are English, the manifest keeps Polish ones.
    private static readonly Dictionary<string, string> EnglishTypes = new(StringComparer.Ordinal)
    {
        ["regulaminy"] = "regulation",
        ["taryfy"] = "tariff",
        ["procedury"] = "procedure",
        ["akty"] = "act",
    };

    private static readonly Dictionary<string, string> EnglishStatuses = new(StringComparer.Ordinal)
    {
        ["obowiazujacy"] = "in-force",
        ["nieaktualny"] = "outdated",
    };

    [Theory]
    [InlineData("REG-06")]
    [InlineData("REG-05")]
    [InlineData("TAR-04")]
    [InlineData("PRO-07")]
    [InlineData("dz-u-2019-1781-ochrona-danych")]
    public async Task ChunkAsync_CorpusDocumentMatchesItsGoldenFile(string id)
    {
        (string pdf, DocumentMetadata metadata) = Entry(id);
        using ServiceProvider provider = new ServiceCollection().AddLegalAgentChunking().BuildServiceProvider();
        await using FileStream stream = File.OpenRead(RepoPaths.Corpus(pdf));

        ChunkedDocument document = await provider.GetRequiredService<IDocumentChunker>().ChunkAsync(stream, metadata, cancellationToken: TestContext.Current.CancellationToken);

        GoldenFile.AssertMatches(ChunkJson.ToJsonLines(document), RepoPaths.Golden(id));
    }

    private static (string Pdf, DocumentMetadata Metadata) Entry(string id)
    {
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(RepoPaths.Corpus("manifest.json")));
        JsonElement d = manifest.RootElement.GetProperty("documents").EnumerateArray().Single(e => e.GetProperty("id").GetString() == id);
        string? Text(string name) => d.TryGetProperty(name, out JsonElement v) ? v.GetString() : null;
        DateOnly? Date(string name) => Text(name) is { } s ? DateOnly.ParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture) : null;
        var metadata = new DocumentMetadata(id)
        {
            Designation = Text("designation"),
            Type = Text("type") is { } type ? EnglishTypes[type] : null,
            Title = Text("title"),
            Version = d.TryGetProperty("version", out JsonElement version) ? version.GetInt32() : null,
            ValidFrom = Date("validFrom"),
            ValidTo = Date("validTo"),
            Status = Text("status") is { } status ? EnglishStatuses[status] : null,
            PreviousVersion = Text("previousVersion"),
        };
        return (Text("pdf")!, metadata);
    }
}
