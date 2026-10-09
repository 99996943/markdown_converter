namespace LegalAgent.Corpus.Output;

/// <summary>A file of the corpus: a path relative to the corpus root (with '/' separators) and its bytes.</summary>
/// <param name="RelativePath">Path relative to the corpus root, '/' separated.</param>
/// <param name="Content">File content.</param>
public sealed record CorpusFile(string RelativePath, byte[] Content);

/// <summary>Result of comparing a corpus on disk with the expected files; all paths are ordinal-sorted.</summary>
/// <param name="Different">Files that exist with different bytes.</param>
/// <param name="Missing">Expected files that are absent.</param>
/// <param name="Extra">Unexpected *.pdf / *.md files under managed directories.</param>
public sealed record CorpusDiff(IReadOnlyList<string> Different, IReadOnlyList<string> Missing, IReadOnlyList<string> Extra);

/// <summary>Writes, cleans up and compares the files of the generated corpus (research R14).</summary>
public static class CorpusWriter
{
    /// <summary>
    /// Writes <paramref name="content"/> to a temporary file in the target directory and moves it over
    /// <paramref name="path"/>; creates the directory. On failure the temporary file is deleted and an existing target is untouched.
    /// </summary>
    public static void WriteAtomic(string path, byte[] content) => throw new NotImplementedException();

    internal static void WriteAtomic(string path, byte[] content, Action<string>? beforeMove) => throw new NotImplementedException();

    /// <summary>UTF-8 without BOM, every CRLF/CR converted to LF, exactly one trailing LF.</summary>
    public static byte[] TextBytes(string text) => throw new NotImplementedException();

    /// <summary>Atomically writes every file under <paramref name="root"/>.</summary>
    public static void Write(string root, IEnumerable<CorpusFile> files) => throw new NotImplementedException();

    /// <summary>
    /// Deletes *.pdf and *.md files (recursively) under the managed directories that are not in the plan and returns
    /// their relative paths (ordinal). Touches nothing else. The caller must invoke it only after a fully successful run.
    /// </summary>
    public static IReadOnlyList<string> Cleanup(string root, IEnumerable<string> plannedRelativePaths, IEnumerable<string> managedDirectories) =>
        throw new NotImplementedException();

    /// <summary>
    /// Compares the corpus on disk with <paramref name="expected"/>: different bytes, missing files and unexpected
    /// *.pdf / *.md files under managed directories.
    /// </summary>
    public static CorpusDiff Compare(string root, IEnumerable<CorpusFile> expected, IEnumerable<string> managedDirectories) =>
        throw new NotImplementedException();
}
