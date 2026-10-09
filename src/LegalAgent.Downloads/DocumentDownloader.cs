using LegalAgent.Downloads.Model;

namespace LegalAgent.Downloads;

/// <summary>Downloads a list of PDF documents in parallel into the output directory (contracts/library-api.md).</summary>
public sealed class DocumentDownloader
{
    private readonly HttpClient httpClient;
    private readonly DownloadOptions options;

    /// <summary>Creates the downloader.</summary>
    /// <param name="httpClient">Client without automatic redirects and with an infinite timeout.</param>
    /// <param name="options">Download options.</param>
    /// <exception cref="ArgumentException">The options are invalid.</exception>
    public DocumentDownloader(HttpClient httpClient, DownloadOptions options)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        this.httpClient = httpClient;
        this.options = options;
    }

    /// <summary>
    /// Downloads all addresses, writes the manifest and, when everything succeeded, removes PDF files outside the
    /// current set. Failures of single addresses are results, not exceptions.
    /// </summary>
    /// <param name="addresses">Addresses (valid according to <see cref="AddressValidator"/>).</param>
    /// <param name="progress">Optional progress events.</param>
    /// <param name="cancellationToken">Cancellation by the user.</param>
    /// <returns>The run result.</returns>
    public Task<DownloadRun> DownloadAllAsync(
        IReadOnlyList<Uri> addresses,
        IProgress<DownloadEvent>? progress = null,
        CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();
}
