using System.Security.Cryptography;
using LegalAgent.Downloads.Model;

namespace LegalAgent.Downloads;

/// <summary>Downloads one address into a <c>.part</c> file and moves it into place (research R3–R6).</summary>
internal sealed class SingleDownload(HttpClient httpClient, DownloadOptions options)
{
    private const int BufferSize = 81920;

    /// <summary>Downloads the planned item into the directory.</summary>
    public async Task<DownloadResult> RunAsync(PlannedDownload item, string directory, CancellationToken cancellationToken)
    {
        string target = Path.Combine(directory, item.FileName);
        string part = target + ".part";
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, item.Address);
            if (!string.IsNullOrWhiteSpace(options.UserAgent))
            {
                request.Headers.TryAddWithoutValidation("User-Agent", options.UserAgent);
            }

            using HttpResponseMessage response = await httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            (long size, string sha256) = await WritePartAsync(response.Content, part, cancellationToken).ConfigureAwait(false);
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
        finally
        {
            File.Delete(part);
        }
    }

    private static async Task<(long Size, string Sha256)> WritePartAsync(HttpContent content, string part, CancellationToken cancellationToken)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Stream body = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using (body.ConfigureAwait(false))
        {
            var file = new FileStream(part, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, useAsync: true);
            await using (file.ConfigureAwait(false))
            {
                byte[] buffer = new byte[BufferSize];
                long size = 0;
                int read;
                while ((read = await body.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    hash.AppendData(buffer, 0, read);
                    await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                    size += read;
                }

                return (size, Convert.ToHexStringLower(hash.GetHashAndReset()));
            }
        }
    }
}
