using System.Text;

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
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// Writes <paramref name="content"/> to a temporary file in the target directory and moves it over
    /// <paramref name="path"/>; creates the directory. On failure the temporary file is deleted and an existing target is untouched.
    /// </summary>
    public static void WriteAtomic(string path, byte[] content) => WriteAtomic(path, content, null);

    internal static void WriteAtomic(string path, byte[] content, Action<string>? beforeMove)
    {
        string full = Path.GetFullPath(path);
        string directory = Path.GetDirectoryName(full)!;
        Directory.CreateDirectory(directory);
        string temp = Path.Combine(directory, "." + Path.GetFileName(full) + "." + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            File.WriteAllBytes(temp, content);
            beforeMove?.Invoke(temp);
            File.Move(temp, full, overwrite: true);
        }
        catch
        {
            try
            {
                File.Delete(temp);
            }
            catch (IOException)
            {
                // Best effort: the original failure is the one that matters.
            }

            throw;
        }
    }

    /// <summary>UTF-8 without BOM, every CRLF/CR converted to LF, exactly one trailing LF.</summary>
    public static byte[] TextBytes(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        string normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        return Utf8NoBom.GetBytes(normalized.TrimEnd('\n') + "\n");
    }

    /// <summary>Atomically writes every file under <paramref name="root"/>.</summary>
    public static void Write(string root, IEnumerable<CorpusFile> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        foreach (CorpusFile file in files)
        {
            WriteAtomic(Absolute(root, file.RelativePath), file.Content);
        }
    }

    /// <summary>
    /// Deletes *.pdf, *.md and *.jsonl (chunk) files (recursively) under the managed directories that are not in the plan and returns
    /// their relative paths (ordinal). Touches nothing else. The caller must invoke it only after a fully successful run.
    /// </summary>
    public static IReadOnlyList<string> Cleanup(string root, IEnumerable<string> plannedRelativePaths, IEnumerable<string> managedDirectories)
    {
        var planned = new HashSet<string>(plannedRelativePaths, StringComparer.Ordinal);
        var deleted = new List<string>();
        foreach (string relative in ManagedFiles(root, managedDirectories))
        {
            if (!planned.Contains(relative))
            {
                File.Delete(Absolute(root, relative));
                deleted.Add(relative);
            }
        }

        return deleted;
    }

    /// <summary>
    /// Compares the corpus on disk with <paramref name="expected"/>: different bytes, missing files and unexpected
    /// *.pdf / *.md / *.jsonl files under managed directories.
    /// </summary>
    public static CorpusDiff Compare(string root, IEnumerable<CorpusFile> expected, IEnumerable<string> managedDirectories)
    {
        var different = new List<string>();
        var missing = new List<string>();
        var expectedPaths = new HashSet<string>(StringComparer.Ordinal);
        foreach (CorpusFile file in expected)
        {
            expectedPaths.Add(file.RelativePath);
            string full = Absolute(root, file.RelativePath);
            if (!File.Exists(full))
            {
                missing.Add(file.RelativePath);
            }
            else if (!File.ReadAllBytes(full).AsSpan().SequenceEqual(file.Content))
            {
                different.Add(file.RelativePath);
            }
        }

        List<string> extra = ManagedFiles(root, managedDirectories).Where(p => !expectedPaths.Contains(p)).ToList();
        different.Sort(StringComparer.Ordinal);
        missing.Sort(StringComparer.Ordinal);
        return new CorpusDiff(different, missing, extra);
    }

    private static string Absolute(string root, string relative) =>
        Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));

    /// <summary>Relative '/'-separated paths of *.pdf, *.md and *.jsonl files under the managed directories, ordinal-sorted.</summary>
    private static List<string> ManagedFiles(string root, IEnumerable<string> managedDirectories)
    {
        var result = new List<string>();
        foreach (string dir in managedDirectories)
        {
            string absolute = Absolute(root, dir);
            if (!Directory.Exists(absolute))
            {
                continue;
            }

            foreach (string file in Directory.EnumerateFiles(absolute, "*", SearchOption.AllDirectories))
            {
                string extension = Path.GetExtension(file);
                if (string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(extension, ".md", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(extension, ".jsonl", StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/'));
                }
            }
        }

        result.Sort(StringComparer.Ordinal);
        return result;
    }
}
