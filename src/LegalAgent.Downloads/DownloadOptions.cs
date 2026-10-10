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

    /// <summary>Throws <see cref="ArgumentException"/> when the options cannot be used.</summary>
    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(OutputDirectory))
        {
            throw new ArgumentException("Katalog pobrań nie może być pusty.", nameof(OutputDirectory));
        }

        if (AllowedHosts is null || AllowedHosts.Count == 0)
        {
            throw new ArgumentException("Lista dozwolonych hostów musi mieć co najmniej jeden wpis.", nameof(AllowedHosts));
        }

        foreach (string host in AllowedHosts)
        {
            if (!IsHostName(host))
            {
                throw new ArgumentException(
                    $"Niepoprawny dozwolony host „{host}”: podaj samą nazwę hosta, bez schematu, portu i ścieżki.",
                    nameof(AllowedHosts));
            }
        }

        if (Timeout <= TimeSpan.Zero)
        {
            throw new ArgumentException("Limit czasu musi być dodatni.", nameof(Timeout));
        }

        if (MaxFileSizeBytes <= 0)
        {
            throw new ArgumentException("Limit rozmiaru pliku musi być dodatni.", nameof(MaxFileSizeBytes));
        }

        if (MaxRedirects is < 0 or > 20)
        {
            throw new ArgumentException("Limit przekierowań musi mieścić się w przedziale 0–20.", nameof(MaxRedirects));
        }
    }

    private static bool IsHostName(string? host) =>
        !string.IsNullOrWhiteSpace(host)
        && Uri.CheckHostName(host) == UriHostNameType.Dns;
}
