namespace LegalAgent.Downloads.Model;

/// <summary>Result of a whole download run.</summary>
/// <param name="Results">Results in index order.</param>
/// <param name="RemovedFiles">Files removed by the cleanup (ordinal order); empty unless everything was downloaded.</param>
/// <param name="ManifestPath">Path of the written manifest.</param>
public sealed record DownloadRun(IReadOnlyList<DownloadResult> Results, IReadOnlyList<string> RemovedFiles, string ManifestPath)
{
    /// <summary>Whether every address was downloaded.</summary>
    public bool AllSucceeded => Results.All(r => r.Status == DownloadStatus.Downloaded);
}

/// <summary>Kind of a progress event.</summary>
public enum DownloadEventKind
{
    /// <summary>The download of an address started.</summary>
    Started,

    /// <summary>The download of an address finished (successfully or not).</summary>
    Finished,
}

/// <summary>Progress of one address; the order between addresses is not deterministic.</summary>
/// <param name="Index">1-based position of the address.</param>
/// <param name="Address">The address.</param>
/// <param name="Kind">Event kind.</param>
/// <param name="Result">The result, for <see cref="DownloadEventKind.Finished"/>.</param>
public sealed record DownloadEvent(int Index, Uri Address, DownloadEventKind Kind, DownloadResult? Result);
