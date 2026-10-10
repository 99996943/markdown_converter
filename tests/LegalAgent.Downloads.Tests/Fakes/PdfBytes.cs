using System.Text;

namespace LegalAgent.Downloads.Tests.Fakes;

/// <summary>Test bodies: minimal PDF-like content and an HTML page.</summary>
internal static class PdfBytes
{
    /// <summary>An HTML error page.</summary>
    public static byte[] Html { get; } = Encoding.UTF8.GetBytes("<!DOCTYPE html><html><body>Nie znaleziono</body></html>");

    /// <summary>A small body starting with the PDF signature, distinct per seed.</summary>
    public static byte[] Sample(string seed) =>
        Encoding.ASCII.GetBytes($"%PDF-1.7\n% {seed}\n1 0 obj << >> endobj\ntrailer << >>\n%%EOF\n");

    /// <summary>A body starting with the PDF signature, of the given size.</summary>
    public static byte[] OfSize(int size)
    {
        byte[] bytes = new byte[size];
        Array.Fill(bytes, (byte)'x');
        Encoding.ASCII.GetBytes("%PDF-1.7\n").CopyTo(bytes, 0);
        return bytes;
    }
}

/// <summary>A non-seekable stream that serves the bytes and then fails or hangs.</summary>
internal sealed class ScriptedStream(byte[] bytes, int failAfter = -1, bool hangAfter = false) : Stream
{
    private int position;

    /// <inheritdoc />
    public override bool CanRead => true;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override bool CanWrite => false;

    /// <inheritdoc />
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) =>
        ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

    /// <inheritdoc />
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        int limit = failAfter >= 0 ? Math.Min(failAfter, bytes.Length) : bytes.Length;
        if (position >= limit)
        {
            if (hangAfter)
            {
                await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
            }

            if (failAfter >= 0)
            {
                throw new IOException("Połączenie zerwane (atrapa).");
            }

            return 0;
        }

        int n = Math.Min(buffer.Length, limit - position);
        bytes.AsMemory(position, n).CopyTo(buffer);
        position += n;
        return n;
    }

    /// <inheritdoc />
    public override void Flush()
    {
    }

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
