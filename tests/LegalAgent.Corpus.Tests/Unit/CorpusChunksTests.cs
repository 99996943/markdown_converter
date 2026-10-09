using LegalAgent.Chunking.Model;
using LegalAgent.Chunking.Serialization;
using LegalAgent.Corpus.Cli;
using LegalAgent.Corpus.Manifest;
using LegalAgent.Corpus.Pdf;
using LegalAgent.Corpus.Tests.Unit.Content;

namespace LegalAgent.Corpus.Tests.Unit;

/// <summary>Spec 004, T038 (FR-261): chunk files of the corpus written by generate/refresh and checked by verify.</summary>
public sealed class CorpusChunksTests : IDisposable
{
    private const string ActId = "dz-u-2025-644-aml";

    // Values that reach the model are English; the manifest keeps its Polish values (contract of spec 003).
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

    private readonly string _root = Path.Combine(Path.GetTempPath(), "corpus-chunks-" + Guid.NewGuid().ToString("N"));

    public CorpusChunksTests()
    {
        foreach (string file in Directory.EnumerateFiles(MiniContent.Path, "*", SearchOption.AllDirectories))
        {
            string target = Path.Combine(_root, "zrodla", Path.GetRelativePath(MiniContent.Path, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }

        WriteParameters(documentsPerType: 2);
        Directory.CreateDirectory(Path.Combine(Out, "akty"));
        byte[] pdf = new SyntheticPdfBuilder()
            .Page()
            .Text(72, 80, "USTAWA", 14, bold: true)
            .Text(72, 120, "o przeciwdziałaniu praniu pieniędzy", 11, bold: true)
            .Text(72, 160, "Art. 1. Ustawa określa zasady i tryb przeciwdziałania praniu pieniędzy.", 11)
            .Build();
        File.WriteAllBytes(Path.Combine(Out, "akty", ActId + ".pdf"), pdf);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private string Out => Path.Combine(_root, "out");

    private string LastError { get; set; } = string.Empty;

    private void WriteParameters(int documentsPerType) => File.WriteAllText(Path.Combine(_root, "przebieg.json"), $$"""
        {
          "seed": 4,
          "documentsPerType": {{documentsPerType}},
          "pages": { "min": 1, "max": 3 },
          "versionedShare": 0,
          "outdatedPerType": 0,
          "contradictionPairsPerType": 0,
          "crossTypeContradictionPairs": 0,
          "strictUniqueness": false,
          "maxSharedShare": 100,
          "outputDirectory": "out",
          "contentDirectory": "zrodla"
        }
        """);

    private int Run(params string[] args)
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        int code = Program.Run([.. args, "--params", Path.Combine(_root, "przebieg.json")], stdout, stderr, _root);
        LastError = stderr.ToString();
        return code;
    }

    private IReadOnlyList<ManifestDocument> Documents() =>
        ManifestWriter.Read(File.ReadAllText(Path.Combine(Out, "manifest.json"))).Documents;

    private string FullPath(string relative) => Path.Combine(Out, relative);

    [Fact]
    public void GenerateAndRefresh_WriteAChunkFileNextToEveryMarkdownWithTheManifestMetadata()
    {
        Assert.Equal(0, Run("generate"));
        Assert.Equal(0, Run("refresh"));

        IReadOnlyList<ManifestDocument> documents = Documents();
        Assert.Contains(documents, d => d.Type == "akty");
        Assert.All(documents, d =>
        {
            Assert.Equal(d.Markdown[..^".md".Length] + ".chunks.jsonl", d.Chunks);
            IReadOnlyList<ChunkRecord> records = ChunkJson.ReadLines(File.ReadAllText(FullPath(d.Chunks!)));
            Assert.NotEmpty(records);
            DocumentMetadata m = records[0].Document.Metadata;
            Assert.Equal(
                (d.Id, d.Designation, EnglishTypes[d.Type], d.Title, d.Version, d.ValidFrom, d.ValidTo, EnglishStatuses[d.Status], d.PreviousVersion),
                (m.DocumentId, m.Designation, m.Type, m.Title, m.Version, m.ValidFrom, m.ValidTo, m.Status, m.PreviousVersion));
            Assert.All(records, r => Assert.StartsWith(d.Id + "_", r.Chunk.ChunkId, StringComparison.Ordinal));
        });
    }

    [Fact]
    public void Refresh_RestoresChangedAndMissingChunkFiles()
    {
        Assert.Equal(0, Run("generate"));
        ManifestDocument[] documents = [.. Documents().Where(d => d.Type != "akty").Take(2)];
        Dictionary<string, byte[]> before = documents.ToDictionary(d => d.Chunks!, d => File.ReadAllBytes(FullPath(d.Chunks!)), StringComparer.Ordinal);
        File.AppendAllText(FullPath(documents[0].Chunks!), "{}\n");
        File.Delete(FullPath(documents[1].Chunks!));

        Assert.Equal(0, Run("refresh"));

        Assert.All(before, kv => Assert.Equal(kv.Value, File.ReadAllBytes(FullPath(kv.Key))));
    }

    [Fact]
    public void Verify_ReportsAChangedAndAMissingChunkFile()
    {
        Assert.Equal(0, Run("generate"));
        Assert.Equal(0, Run("verify"));
        ManifestDocument[] documents = [.. Documents().Where(d => d.Type != "akty").Take(2)];
        string[] lines = File.ReadAllLines(FullPath(documents[0].Chunks!));
        lines[0] = lines[0].Replace("\"part\":1", "\"part\":7", StringComparison.Ordinal);
        File.WriteAllText(FullPath(documents[0].Chunks!), string.Join('\n', lines) + "\n");
        File.Delete(FullPath(documents[1].Chunks!));

        Assert.Equal(1, Run("verify"));

        Assert.Contains(documents[0].Chunks!, LastError, StringComparison.Ordinal);
        Assert.Contains(documents[1].Chunks!, LastError, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_RemovesTheChunkFileOfADocumentThatIsGone()
    {
        Assert.Equal(0, Run("generate"));
        ManifestDocument gone = Documents().Single(d => d.Id == "REG-02");
        Assert.True(File.Exists(FullPath(gone.Chunks!)));

        WriteParameters(documentsPerType: 1);
        Assert.Equal(0, Run("generate"));

        Assert.False(File.Exists(FullPath(gone.Chunks!)));
    }
}
