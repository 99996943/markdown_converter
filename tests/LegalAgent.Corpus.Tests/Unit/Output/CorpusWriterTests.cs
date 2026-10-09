using System.Text;
using LegalAgent.Corpus.Output;

namespace LegalAgent.Corpus.Tests.Unit.Output;

public sealed class CorpusWriterTests : IDisposable
{
    private static readonly string[] Managed = ["regulaminy", "taryfy", "procedury", "zatrute"];

    private readonly string _root = Path.Combine(Path.GetTempPath(), "corpus-writer-" + Guid.NewGuid().ToString("N"));

    public CorpusWriterTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private string P(string relative) => Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar));

    private void Put(string relative, string content = "x")
    {
        Directory.CreateDirectory(Path.GetDirectoryName(P(relative))!);
        File.WriteAllText(P(relative), content);
    }

    private static CorpusFile F(string path, string content) => new(path, Encoding.UTF8.GetBytes(content));

    [Fact]
    public void WriteAtomic_CreatesDirectoryAndOverwrites()
    {
        string path = P("a/b/file.md");
        CorpusWriter.WriteAtomic(path, [1, 2, 3]);
        CorpusWriter.WriteAtomic(path, [4, 5]);
        Assert.Equal(new byte[] { 4, 5 }, File.ReadAllBytes(path));
        Assert.Single(Directory.GetFileSystemEntries(P("a/b")));
    }

    [Fact]
    public void WriteAtomic_FailureBeforeMove_KeepsOldContentAndLeavesNoTempFile()
    {
        string path = P("d/file.md");
        CorpusWriter.WriteAtomic(path, [1, 2, 3]);

        Assert.Throws<InvalidOperationException>(() =>
            CorpusWriter.WriteAtomic(path, [9, 9], _ => throw new InvalidOperationException("boom")));

        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(path));
        Assert.Equal([path], Directory.GetFileSystemEntries(P("d")));
    }

    [Fact]
    public void TextBytes_Utf8NoBomLfAndSingleTrailingNewline()
    {
        byte[] bytes = CorpusWriter.TextBytes("zażółć\r\nlinia\rkoniec\r\n\r\n");
        Assert.NotEqual(0xEF, bytes[0]);
        Assert.Equal("zażółć\nlinia\nkoniec\n", Encoding.UTF8.GetString(bytes));
    }

    [Fact]
    public void TextBytes_AddsTrailingNewlineWhenMissing()
    {
        Assert.Equal("a\n", Encoding.UTF8.GetString(CorpusWriter.TextBytes("a")));
    }

    [Fact]
    public void Write_WritesAllFilesUnderRoot()
    {
        CorpusWriter.Write(_root, [F("regulaminy/a.md", "A"), F("manifest.json", "M")]);
        Assert.Equal("A", File.ReadAllText(P("regulaminy/a.md")));
        Assert.Equal("M", File.ReadAllText(P("manifest.json")));
    }

    [Fact]
    public void Cleanup_DeletesOnlyUnplannedPdfAndMdInManagedDirectories()
    {
        Put("regulaminy/keep.pdf");
        Put("regulaminy/old.pdf");
        Put("regulaminy/old.md");
        Put("taryfy/old.md");
        Put("zatrute/taryfy/podszywanie/x.pdf");
        Put("zatrute/taryfy/podszywanie/keep.md");
        Put("procedury/notes.txt");
        Put("README.md");
        Put("przebieg.json");
        Put("zrodla/s.pdf");
        Put("zrodla/s.md");
        Put("akty/ustawa.pdf");
        Put("inne/old.pdf");

        IReadOnlyList<string> deleted = CorpusWriter.Cleanup(
            _root, ["regulaminy/keep.pdf", "zatrute/taryfy/podszywanie/keep.md"], Managed);

        Assert.Equal(
            ["regulaminy/old.md", "regulaminy/old.pdf", "taryfy/old.md", "zatrute/taryfy/podszywanie/x.pdf"],
            deleted);
        Assert.True(File.Exists(P("regulaminy/keep.pdf")));
        Assert.True(File.Exists(P("zatrute/taryfy/podszywanie/keep.md")));
        Assert.False(File.Exists(P("zatrute/taryfy/podszywanie/x.pdf")));
        foreach (string kept in new[] { "procedury/notes.txt", "README.md", "przebieg.json", "zrodla/s.pdf", "zrodla/s.md", "akty/ustawa.pdf", "inne/old.pdf" })
        {
            Assert.True(File.Exists(P(kept)), kept);
        }
    }

    [Fact]
    public void Cleanup_MissingManagedDirectory_IsIgnored()
    {
        Assert.Empty(CorpusWriter.Cleanup(_root, [], Managed));
    }

    [Fact]
    public void Compare_ReportsDifferentMissingAndExtra()
    {
        Put("regulaminy/same.md", "S");
        Put("regulaminy/diff.md", "old");
        Put("regulaminy/zextra.pdf");
        Put("taryfy/aextra.md");
        Put("taryfy/ignored.txt");
        Put("manifest.json", "old");
        Put("README.md");

        CorpusDiff diff = CorpusWriter.Compare(
            _root,
            [F("regulaminy/same.md", "S"), F("regulaminy/diff.md", "new"), F("procedury/missing.pdf", "m"), F("manifest.json", "new"), F("zatrute/b.md", "b")],
            Managed);

        Assert.Equal(["manifest.json", "regulaminy/diff.md"], diff.Different);
        Assert.Equal(["procedury/missing.pdf", "zatrute/b.md"], diff.Missing);
        Assert.Equal(["regulaminy/zextra.pdf", "taryfy/aextra.md"], diff.Extra);
    }

    [Fact]
    public void Compare_IdenticalCorpus_IsEmpty()
    {
        Put("regulaminy/a.md", "A");
        CorpusDiff diff = CorpusWriter.Compare(_root, [F("regulaminy/a.md", "A")], Managed);
        Assert.Empty(diff.Different);
        Assert.Empty(diff.Missing);
        Assert.Empty(diff.Extra);
    }
}
