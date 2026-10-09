using LegalAgent.Corpus.Manifest;
using ManifestModel = LegalAgent.Corpus.Manifest.Manifest;

namespace LegalAgent.Corpus.Tests.Unit.Manifest;

/// <summary>Spec 004, T036: the manifest points to the chunk file of every document.</summary>
public sealed class ManifestChunksTests
{
    private static readonly ManifestDocument Reg01 = new(
        "REG-01", "regulaminy", "Regulamin rachunków", "BP/REG/01", 1, new DateOnly(2025, 1, 1), null, "obowiazujacy", null,
        "regulaminy/REG-01.pdf", "regulaminy/REG-01.md", 12)
    {
        Chunks = "regulaminy/REG-01.chunks.jsonl",
    };

    private static ManifestModel Sample(params ManifestDocument[] documents) => new(
        new ManifestRun(7UL, new DateOnly(2026, 1, 15), "{}", "1.2.3", "0.1.0", "abc123"),
        documents);

    [Fact]
    public void Write_PutsChunksRightAfterMarkdown()
    {
        string json = ManifestWriter.Write(Sample(Reg01), ["regulaminy"]).ReplaceLineEndings("\n");

        Assert.Matches(
            "\"markdown\": \"regulaminy/REG-01\\.md\",\\n *\"chunks\": \"regulaminy/REG-01\\.chunks\\.jsonl\",\\n *\"pages\": 12",
            json);
        Assert.Contains("\"schemaVersion\": 1,", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_ReturnsTheChunkPath()
    {
        ManifestModel manifest = ManifestWriter.Read(ManifestWriter.Write(Sample(Reg01), ["regulaminy"]));

        Assert.Equal("regulaminy/REG-01.chunks.jsonl", Assert.Single(manifest.Documents).Chunks);
    }

    [Fact]
    public void WriteAndRead_DocumentWithoutChunksHasNoField()
    {
        ManifestDocument without = Reg01 with { Chunks = null };

        string json = ManifestWriter.Write(Sample(without), ["regulaminy"]);

        Assert.DoesNotContain("\"chunks\"", json, StringComparison.Ordinal);
        Assert.Null(Assert.Single(ManifestWriter.Read(json).Documents).Chunks);
    }
}
