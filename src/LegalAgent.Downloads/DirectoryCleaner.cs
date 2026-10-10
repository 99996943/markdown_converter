namespace LegalAgent.Downloads;

/// <summary>Removes PDF files outside the current set and leftover <c>.part</c> files (FR-325, research R9).</summary>
internal static class DirectoryCleaner
{
    /// <summary>Removes the files; only the top level of the directory is touched.</summary>
    /// <param name="directory">Output directory.</param>
    /// <param name="keep">Names of the current files (compared without case).</param>
    /// <returns>Names of the removed files, ordinal order.</returns>
    /// <exception cref="DownloadDirectoryException">A file cannot be removed.</exception>
    public static IReadOnlyList<string> RemoveStale(string directory, IEnumerable<string> keep)
    {
        var current = new HashSet<string>(keep, StringComparer.OrdinalIgnoreCase);
        string[] stale =
        [
            .. Directory.EnumerateFiles(directory)
                .Select(path => Path.GetFileName(path))
                .Where(name => !current.Contains(name)
                    && (name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".part", StringComparison.OrdinalIgnoreCase)))
                .Order(StringComparer.Ordinal),
        ];

        foreach (string name in stale)
        {
            try
            {
                File.Delete(Path.Combine(directory, name));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                throw new DownloadDirectoryException($"Nie można usunąć pliku {name} z katalogu pobrań: {e.Message}", e);
            }
        }

        return stale;
    }
}
