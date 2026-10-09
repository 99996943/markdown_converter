namespace LegalAgent.Downloads;

/// <summary>Options of a download run. The library knows no sources: hosts and addresses come from the caller.</summary>
public sealed record DownloadOptions
{
    /// <summary>Directory the files and the manifest are written to (relative paths resolve against the current directory).</summary>
    public string OutputDirectory { get; init; } = "downloads";

    /// <summary>Allowed host names; a host matches an entry when it equals it or is its subdomain.</summary>
    public IReadOnlyList<string> AllowedHosts { get; init; } = [];

    /// <summary>Whether plain <c>http</c> addresses are accepted (otherwise only <c>https</c>).</summary>
    public bool AllowHttp { get; init; }

    /// <summary>Time limit of one whole download (redirects, headers, body, write).</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary>Maximum size of one file in bytes.</summary>
    public long MaxFileSizeBytes { get; init; } = 50L * 1024 * 1024;

    /// <summary>Maximum number of followed redirects (0–20).</summary>
    public int MaxRedirects { get; init; } = 5;

    /// <summary>Value of the <c>User-Agent</c> request header, if any.</summary>
    public string? UserAgent { get; init; }
}
