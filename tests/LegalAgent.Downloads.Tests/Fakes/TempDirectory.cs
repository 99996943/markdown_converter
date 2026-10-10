namespace LegalAgent.Downloads.Tests.Fakes;

/// <summary>A fresh directory under the system temp path, removed on dispose.</summary>
internal sealed class TempDirectory : IDisposable
{
    /// <summary>Creates the directory.</summary>
    public TempDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "legalagent-dl-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    /// <summary>Full path of the directory.</summary>
    public string Path { get; }

    /// <summary>Path of a child entry.</summary>
    public string Combine(params string[] parts) => System.IO.Path.Combine([Path, .. parts]);

    /// <summary>Names of the files directly in the directory (ordinal order).</summary>
    public IReadOnlyList<string> FileNames(string? subdirectory = null) =>
        [.. Directory.GetFiles(subdirectory is null ? Path : Combine(subdirectory))
            .Select(f => System.IO.Path.GetFileName(f))
            .Order(StringComparer.Ordinal)];

    /// <inheritdoc />
    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
            // Best effort cleanup.
        }
        catch (UnauthorizedAccessException)
        {
            // Best effort cleanup.
        }
    }
}
