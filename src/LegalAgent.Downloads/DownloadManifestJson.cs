using LegalAgent.Downloads.Model;

namespace LegalAgent.Downloads;

/// <summary>Serializes the download manifest (contracts/download-manifest.md).</summary>
public static class DownloadManifestJson
{
    /// <summary>Manifest schema version.</summary>
    public const int SchemaVersion = 1;

    /// <summary>Serializes the results to the manifest text (LF, trailing newline).</summary>
    /// <param name="results">Results in index order.</param>
    /// <returns>The manifest JSON.</returns>
    public static string Serialize(IReadOnlyList<DownloadResult> results) => throw new NotImplementedException();
}
