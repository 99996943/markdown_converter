using System.Globalization;
using LegalAgent.Downloads.Model;

namespace LegalAgent.Downloads;

/// <summary>Downloads a list of PDF documents in parallel into the output directory (contracts/library-api.md).</summary>
public sealed class DocumentDownloader
{
    /// <summary>File name of the manifest in the output directory.</summary>
    public const string ManifestFileName = "manifest.json";

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
        options.Validate();
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
    /// <exception cref="ArgumentException">The list is empty or contains an invalid address.</exception>
    /// <exception cref="DownloadDirectoryException">The output directory cannot be used.</exception>
    public async Task<DownloadRun> DownloadAllAsync(
        IReadOnlyList<Uri> addresses,
        IProgress<DownloadEvent>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(addresses);
        ValidateAddresses(addresses);
        cancellationToken.ThrowIfCancellationRequested();

        string directory = Path.GetFullPath(options.OutputDirectory);
        CreateDirectory(directory);

        IReadOnlyList<PlannedDownload> plan = FileNamePlanner.Plan(addresses);
        var single = new SingleDownload(httpClient, options);
        var results = new List<DownloadResult>(plan.Count);
        foreach (PlannedDownload item in plan)
        {
            results.Add(await single.RunAsync(item, directory, cancellationToken).ConfigureAwait(false));
        }

        return new DownloadRun(results, [], Path.Combine(directory, ManifestFileName));
    }

    private void ValidateAddresses(IReadOnlyList<Uri> addresses)
    {
        if (addresses.Count == 0)
        {
            throw new ArgumentException("Lista adresów jest pusta.", nameof(addresses));
        }

        IReadOnlyList<AddressCheck> checks = AddressValidator.CheckAll([.. addresses.Select(a => a.OriginalString)], options);
        for (int i = 0; i < checks.Count; i++)
        {
            if (!checks[i].IsValid)
            {
                throw new ArgumentException(
                    string.Create(CultureInfo.InvariantCulture, $"Adres {i + 1}: {checks[i].Message}"),
                    nameof(addresses));
            }
        }
    }

    private static void CreateDirectory(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw new DownloadDirectoryException($"Nie można utworzyć katalogu pobrań {directory}: {e.Message}", e);
        }
    }
}
