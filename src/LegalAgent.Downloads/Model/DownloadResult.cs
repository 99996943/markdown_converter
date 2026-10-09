namespace LegalAgent.Downloads.Model;

/// <summary>Outcome of one download.</summary>
public enum DownloadStatus
{
    /// <summary>Downloaded and written.</summary>
    Downloaded,

    /// <summary>Failed; see <see cref="DownloadResult.Error"/>.</summary>
    Failed,
}

/// <summary>Result of downloading one address.</summary>
public sealed record DownloadResult
{
    /// <summary>1-based position of the address.</summary>
    public required int Index { get; init; }

    /// <summary>Address given by the user (not the redirect target).</summary>
    public required Uri Address { get; init; }

    /// <summary>Target file name from the plan.</summary>
    public required string FileName { get; init; }

    /// <summary>Outcome.</summary>
    public required DownloadStatus Status { get; init; }

    /// <summary>Size in bytes, when downloaded.</summary>
    public long? SizeBytes { get; init; }

    /// <summary>SHA-256 of the content (64 lower-case hex characters), when downloaded.</summary>
    public string? Sha256 { get; init; }

    /// <summary><c>Last-Modified</c> reported by the server (UTC), when downloaded and present.</summary>
    public DateTimeOffset? LastModified { get; init; }

    /// <summary>The failure, when failed.</summary>
    public DownloadError? Error { get; init; }
}
