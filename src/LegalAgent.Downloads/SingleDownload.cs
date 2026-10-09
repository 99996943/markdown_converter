using System.Globalization;
using System.Security.Cryptography;
using LegalAgent.Downloads.Model;

namespace LegalAgent.Downloads;

/// <summary>Downloads one address into a <c>.part</c> file and moves it into place (research R3–R6).</summary>
internal sealed class SingleDownload(HttpClient httpClient, DownloadOptions options)
{
    private const int BufferSize = 81920;
    private static readonly byte[] PdfSignature = "%PDF-"u8.ToArray();
    private static readonly CultureInfo Polish = CultureInfo.GetCultureInfo("pl-PL");
    private readonly HostAllowList hosts = new(options.AllowedHosts);

    /// <summary>Downloads the planned item into the directory; failures become results.</summary>
    /// <exception cref="OperationCanceledException">Cancelled by the user.</exception>
    public async Task<DownloadResult> RunAsync(PlannedDownload item, string directory, CancellationToken cancellationToken)
    {
        string target = Path.Combine(directory, item.FileName);
        string part = target + ".part";

        // One limit for the whole download: headers, body and write (research R4).
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(options.Timeout);
        CancellationToken token = limit.Token;
        try
        {
            using HttpResponseMessage response = await SendFollowingRedirectsAsync(item.Address, token).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                int code = (int)response.StatusCode;
                throw new DownloadFailureException(new DownloadError(
                    DownloadErrorKind.HttpStatus,
                    string.Create(CultureInfo.InvariantCulture, $"serwer zwrócił {code} {StandardReasonPhrase(response.StatusCode)}"),
                    code));
            }

            if (response.Content.Headers.ContentLength > options.MaxFileSizeBytes)
            {
                throw TooLarge();
            }

            (long size, string sha256) = await WritePartAsync(response.Content, part, token).ConfigureAwait(false);
            WriteGuard(() => File.Move(part, target, overwrite: true));

            return new DownloadResult
            {
                Index = item.Index,
                Address = item.Address,
                FileName = item.FileName,
                Status = DownloadStatus.Downloaded,
                SizeBytes = size,
                Sha256 = sha256,
                LastModified = response.Content.Headers.LastModified?.ToUniversalTime(),
            };
        }
        catch (DownloadFailureException failure)
        {
            return Failed(item, failure.Error);
        }
        catch (OperationCanceledException e) when (!cancellationToken.IsCancellationRequested)
        {
            // Our limit elapsed (or the client's own timeout fired): not a cancellation by the user.
            return Failed(item, new DownloadError(DownloadErrorKind.Timeout, TimeoutMessage(options.Timeout), Detail: e.Message));
        }
        catch (HttpRequestException e)
        {
            return Failed(item, new DownloadError(
                DownloadErrorKind.Connection,
                $"nie można połączyć się z serwerem {item.Address.Host}",
                Detail: e.Message));
        }
        catch (IOException e)
        {
            // Write errors are already WriteFailed; an I/O error here comes from the response body.
            return Failed(item, new DownloadError(
                DownloadErrorKind.Connection,
                $"połączenie z serwerem {item.Address.Host} zostało przerwane podczas pobierania",
                Detail: e.Message));
        }
        finally
        {
            DeletePart(part);
        }
    }

    private static void DeletePart(string part)
    {
        try
        {
            if (File.Exists(part))
            {
                File.Delete(part);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // A leftover is overwritten by the next run and removed by the cleanup (research R9).
        }
    }

    /// <summary>Runs a file-system operation, turning its errors into <see cref="DownloadErrorKind.WriteFailed"/>.</summary>
    private static T WriteGuard<T>(Func<T> write)
    {
        try
        {
            return write();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw WriteFailed(e);
        }
    }

    private static void WriteGuard(Action write) => WriteGuard(() =>
    {
        write();
        return true;
    });

    private static DownloadFailureException WriteFailed(Exception e) =>
        new(new DownloadError(DownloadErrorKind.WriteFailed, "nie można zapisać pliku w katalogu pobrań", Detail: e.Message));

    /// <summary>Message for an elapsed time limit (pl-PL number format).</summary>
    internal static string TimeoutMessage(TimeSpan timeout) =>
        string.Create(Polish, $"przekroczono limit czasu {timeout.TotalSeconds:0.##} s");

    /// <summary>Message for a file above the size limit (pl-PL number format).</summary>
    internal static string TooLargeMessage(long maxBytes) =>
        string.Create(Polish, $"plik przekracza limit rozmiaru {maxBytes / (1024.0 * 1024.0):0.##} MB");

    private static DownloadResult Failed(PlannedDownload item, DownloadError error) => new()
    {
        Index = item.Index,
        Address = item.Address,
        FileName = item.FileName,
        Status = DownloadStatus.Failed,
        Error = error,
    };

    /// <summary>Canonical reason phrase of a status code (servers may send none or their own).</summary>
    private static string? StandardReasonPhrase(System.Net.HttpStatusCode code)
    {
        using var canonical = new HttpResponseMessage(code);
        return canonical.ReasonPhrase;
    }

    private static async Task<int> ReadAtLeastAsync(Stream body, byte[] buffer, int minimum, CancellationToken cancellationToken)
    {
        int total = 0;
        while (total < minimum)
        {
            int read = await body.ReadAsync(buffer.AsMemory(total), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            total += read;
        }

        return total;
    }

    private static bool IsRedirect(System.Net.HttpStatusCode code) =>
        (int)code is 301 or 302 or 303 or 307 or 308;

    /// <summary>
    /// Sends the request and follows redirects manually, checking every target against the scheme rule and the
    /// allowed hosts before any request is sent to it (research R3).
    /// </summary>
    private async Task<HttpResponseMessage> SendFollowingRedirectsAsync(Uri address, CancellationToken cancellationToken)
    {
        Uri current = address;
        for (int redirects = 0; ; redirects++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, current);
            if (!string.IsNullOrWhiteSpace(options.UserAgent))
            {
                request.Headers.TryAddWithoutValidation("User-Agent", options.UserAgent);
            }

            HttpResponseMessage response = await httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            if (!IsRedirect(response.StatusCode) || response.Headers.Location is not { } location)
            {
                return response;
            }

            response.Dispose();
            Uri next = location.IsAbsoluteUri ? location : new Uri(current, location);
            if (redirects >= options.MaxRedirects)
            {
                throw new DownloadFailureException(new DownloadError(
                    DownloadErrorKind.TooManyRedirects,
                    string.Create(CultureInfo.InvariantCulture, $"za dużo przekierowań (limit {options.MaxRedirects})")));
            }

            if (!AddressValidator.IsSchemeAllowed(next, options))
            {
                throw new DownloadFailureException(new DownloadError(
                    DownloadErrorKind.RedirectNotAllowed,
                    $"przekierowanie na niedozwolony adres {next.Scheme}://{next.Host}"));
            }

            if (next.UserInfo.Length > 0 || !hosts.IsAllowed(next))
            {
                throw new DownloadFailureException(new DownloadError(
                    DownloadErrorKind.RedirectNotAllowed,
                    $"przekierowanie na niedozwolony host {next.Host}"));
            }

            current = next;
        }
    }

    private DownloadFailureException TooLarge() =>
        new(new DownloadError(DownloadErrorKind.TooLarge, TooLargeMessage(options.MaxFileSizeBytes)));

    private async Task<(long Size, string Sha256)> WritePartAsync(HttpContent content, string part, CancellationToken cancellationToken)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Stream body = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using (body.ConfigureAwait(false))
        {
            byte[] buffer = new byte[BufferSize];
            int head = await ReadAtLeastAsync(body, buffer, PdfSignature.Length, cancellationToken).ConfigureAwait(false);
            if (head < PdfSignature.Length || !buffer.AsSpan(0, PdfSignature.Length).SequenceEqual(PdfSignature))
            {
                throw new DownloadFailureException(new DownloadError(DownloadErrorKind.NotPdf, "pod adresem nie ma pliku PDF"));
            }

            FileStream file = WriteGuard(() => new FileStream(part, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, useAsync: true));
            await using (file.ConfigureAwait(false))
            {
                long size = 0;
                int read = head;
                while (read > 0)
                {
                    size += read;
                    if (size > options.MaxFileSizeBytes)
                    {
                        throw TooLarge();
                    }

                    hash.AppendData(buffer, 0, read);
                    try
                    {
                        await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                    {
                        throw WriteFailed(e);
                    }

                    read = await body.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                }

                return (size, Convert.ToHexStringLower(hash.GetHashAndReset()));
            }
        }
    }
}

/// <summary>Ends one download with the given error (caught by <see cref="SingleDownload"/>).</summary>
internal sealed class DownloadFailureException(DownloadError error) : Exception(error.Message)
{
    /// <summary>The failure.</summary>
    public DownloadError Error { get; } = error;
}
