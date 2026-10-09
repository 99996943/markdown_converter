using System.Text.Json;
using LegalAgent.Chunking.Serialization;
using LegalAgent.Chunking.Tests.Fixtures;

namespace LegalAgent.Chunking.Tests.Corpus;

/// <summary>
/// T045 (SC-040 – SC-042, SC-044): the committed chunk files of the whole corpus — present for every manifest entry,
/// readable by the contract, covering every word of the document's Markdown, within the limit or marked, with ids
/// unique across the corpus. Runs when <c>LEGALAGENT_CORPUS_FULL</c> is set (CI).
/// </summary>
[Trait("Category", "CorpusFull")]
public sealed class CorpusChunksFullTests
{
    private const int Limit = 2000;

    [Fact]
    public void EveryDocumentHasValidChunksCoveringItsMarkdown()
    {
        if (Environment.GetEnvironmentVariable("LEGALAGENT_CORPUS_FULL") is null)
        {
            Assert.Skip("LEGALAGENT_CORPUS_FULL is not set.");
        }

        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(RepoPaths.Corpus("manifest.json")));
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var duplicates = new List<string>();
        int documents = 0;
        foreach (JsonElement entry in manifest.RootElement.GetProperty("documents").EnumerateArray())
        {
            documents++;
            string id = entry.GetProperty("id").GetString()!;
            Assert.True(entry.TryGetProperty("chunks", out JsonElement path), $"{id}: brak pola chunks w manifeście");
            IReadOnlyList<ChunkRecord> records = ChunkJson.ReadLines(File.ReadAllText(RepoPaths.Corpus(path.GetString()!)));

            Assert.True(records.Count > 0, $"{id}: brak fragmentów");
            Assert.All(records, r => Assert.Equal(id, r.Document.Metadata.DocumentId));
            Assert.All(records, r => Assert.True(r.Chunk.Length <= Limit || r.Chunk.ExceedsLimit, $"{r.Chunk.ChunkId}: {r.Chunk.Length} znaków bez exceedsLimit"));
            duplicates.AddRange(records.Select(r => r.Chunk.ChunkId).Where(c => !ids.Add(c)));

            string markdown = File.ReadAllText(RepoPaths.Corpus(entry.GetProperty("markdown").GetString()!));
            WordCoverage.AssertCovers(markdown, records[0].Document.DetectedTitle is not null, [.. records.Select(r => r.Chunk)]);
        }

        Assert.True(documents >= 86, $"{documents} dokumentów w manifeście");
        Assert.Empty(duplicates);
    }
}
