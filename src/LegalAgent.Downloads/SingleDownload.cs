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
            using var request = new HttpRequestMessage(HttpMethod.Get, item.Address);
            if (!string.IsNullOrWhiteSpace(options.UserAgent))
            {
                request.Headers.TryAddWithoutValidation("User-Agent", options.UserAgent);
            }

            using HttpResponseMessage response = await httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token)
                .ConfigureAwait(false);

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
            File.Move(part, target, overwrite: true);

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
        finally
        {
            File.Delete(part);
        }
    }

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

            var file = new FileStream(part, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, useAsync: true);
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
                    await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
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
