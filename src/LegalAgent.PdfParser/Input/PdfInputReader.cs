using System.Security.Cryptography;

namespace LegalAgent.PdfParser.Input;

/// <summary>Bytes read from the caller's stream together with their hash.</summary>
/// <param name="Bytes">The whole input.</param>
/// <param name="ByteLength">Number of bytes read.</param>
/// <param name="Sha256">Lowercase hexadecimal SHA-256 of the input.</param>
internal sealed record PdfInput(byte[] Bytes, long ByteLength, string Sha256);

/// <summary>Buffers the input stream with a size limit and validates the PDF header (research R4).</summary>
internal static class PdfInputReader
{
    private static readonly byte[] Header = "%PDF-"u8.ToArray();

    /// <summary>
    /// Reads <paramref name="stream"/> from its current position to the end without closing it.
    /// Reading stops one byte past <paramref name="maxInputBytes"/>.
    /// </summary>
    /// <param name="stream">Readable stream (seekable or not).</param>
    /// <param name="maxInputBytes">Size limit in bytes; null disables the limit.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is null.</exception>
    /// <exception cref="ArgumentException">The stream is not readable.</exception>
    /// <exception cref="InvalidPdfException">The stream is empty or does not start with a PDF header.</exception>
    /// <exception cref="PdfLimitExceededException">The input exceeds <paramref name="maxInputBytes"/>.</exception>
    public static async Task<PdfInput> ReadAsync(Stream stream, long? maxInputBytes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead)
        {
            throw new ArgumentException("Strumień wejściowy musi umożliwiać odczyt.", nameof(stream));
        }

        byte[] buffer = new byte[81_920];
        long total = 0;
        using var memory = new MemoryStream();

        while (true)
        {
            int want = buffer.Length;
            if (maxInputBytes is long limit)
            {
                long remaining = limit + 1 - total;
                if (remaining <= 0)
                {
                    break;
                }

                want = (int)Math.Min(want, remaining);
            }

            int read = await stream.ReadAsync(buffer.AsMemory(0, want), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            memory.Write(buffer, 0, read);
            total += read;

            if (maxInputBytes is long max && total > max)
            {
                throw new PdfLimitExceededException(PdfLimit.InputSize, max, total);
            }
        }

        if (total == 0)
        {
            throw new InvalidPdfException(InvalidPdfReason.Empty);
        }

        byte[] bytes = memory.ToArray();
        if (!HasHeader(bytes))
        {
            throw new InvalidPdfException(InvalidPdfReason.NotPdf);
        }

        return new PdfInput(bytes, total, Convert.ToHexStringLower(SHA256.HashData(bytes)));
    }

    /// <summary>The PDF specification tolerates arbitrary bytes before the header within the first 1024 bytes.</summary>
    private const int HeaderSearchWindow = 1024;

    private static bool HasHeader(byte[] bytes) =>
        bytes.AsSpan(0, Math.Min(bytes.Length, HeaderSearchWindow + Header.Length)).IndexOf(Header) is >= 0 and <= HeaderSearchWindow;
}
