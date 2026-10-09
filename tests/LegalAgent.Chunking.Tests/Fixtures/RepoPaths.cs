namespace LegalAgent.Chunking.Tests.Fixtures;

/// <summary>Locations in the repository: its root, the committed corpus and this project's golden files.</summary>
public static class RepoPaths
{
    /// <summary>Directory containing <c>LegalAgent.slnx</c>.</summary>
    public static string Root()
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "LegalAgent.slnx")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException("Repository root not found.");
    }

    /// <summary>A path below <c>corpus/</c>, for example <c>regulaminy/REG-06.pdf</c>.</summary>
    public static string Corpus(string relative) => Path.Combine(Root(), "corpus", relative);

    /// <summary>A golden chunk file in the source tree: <c>tests/LegalAgent.Chunking.Tests/Golden/&lt;id&gt;.chunks.jsonl</c>.</summary>
    public static string Golden(string documentId) =>
        Path.Combine(Root(), "tests", "LegalAgent.Chunking.Tests", "Golden", documentId + ".chunks.jsonl");
}
