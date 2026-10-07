using System.Security.Cryptography;
using System.Text;
using LegalAgent.PdfParser.Input;

namespace LegalAgent.PdfParser.Tests.Unit.Input;

public sealed class PdfInputReaderTests
{
    private static byte[] PdfLike(int totalLength)
    {
        byte[] data = new byte[totalLength];
        Encoding.ASCII.GetBytes("%PDF-1.7\n").CopyTo(data, 0);
        for (int i = 9; i < totalLength; i++)
        {
            data[i] = (byte)'a';
        }

        return data;
    }

    [Fact]
    public async Task ReadAsync_EmptyStream_ThrowsInvalidPdfEmpty()
    {
        using var stream = new MemoryStream();
        InvalidPdfException ex = await Assert.ThrowsAsync<InvalidPdfException>(
            () => PdfInputReader.ReadAsync(stream, null, TestContext.Current.CancellationToken));
        Assert.Equal(InvalidPdfReason.Empty, ex.Reason);
    }

    [Fact]
    public async Task ReadAsync_NoPdfHeader_ThrowsInvalidPdfNotPdf()
    {
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes("Hello, this is not a PDF"));
        InvalidPdfException ex = await Assert.ThrowsAsync<InvalidPdfException>(
            () => PdfInputReader.ReadAsync(stream, null, TestContext.Current.CancellationToken));
        Assert.Equal(InvalidPdfReason.NotPdf, ex.Reason);
    }

    [Fact]
    public async Task ReadAsync_HeaderPrecededByJunkWithinFirst1024Bytes_IsAccepted()
    {
        // The PDF specification tolerates bytes before %PDF- (e.g. mail or HTTP artefacts) within the first 1024 bytes.
        byte[] data = [.. Encoding.ASCII.GetBytes(new string('x', 500)), .. PdfLike(64)];
        using var stream = new MemoryStream(data);

        PdfInput input = await PdfInputReader.ReadAsync(stream, null, TestContext.Current.CancellationToken);

        Assert.Equal(data.Length, input.ByteLength);
    }

    [Fact]
    public async Task ReadAsync_HeaderAfterFirst1024Bytes_ThrowsNotPdf()
    {
        byte[] data = [.. Encoding.ASCII.GetBytes(new string('x', 1100)), .. PdfLike(64)];
        using var stream = new MemoryStream(data);

        InvalidPdfException ex = await Assert.ThrowsAsync<InvalidPdfException>(
            () => PdfInputReader.ReadAsync(stream, null, TestContext.Current.CancellationToken));
        Assert.Equal(InvalidPdfReason.NotPdf, ex.Reason);
    }

    [Fact]
    public async Task ReadAsync_ShortGarbageShorterThanHeader_ThrowsNotPdf()
    {
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes("%PD"));
        InvalidPdfException ex = await Assert.ThrowsAsync<InvalidPdfException>(
            () => PdfInputReader.ReadAsync(stream, null, TestContext.Current.CancellationToken));
        Assert.Equal(InvalidPdfReason.NotPdf, ex.Reason);
    }

    [Fact]
    public async Task ReadAsync_ReadsFromCurrentPositionToEnd()
    {
        byte[] pdf = PdfLike(100);
        byte[] withPrefix = [.. Encoding.ASCII.GetBytes("JUNK"), .. pdf];
        using var stream = new MemoryStream(withPrefix) { Position = 4 };

        PdfInput input = await PdfInputReader.ReadAsync(stream, null, TestContext.Current.CancellationToken);

        Assert.Equal(pdf, input.Bytes);
        Assert.Equal(100, input.ByteLength);
    }

    [Fact]
    public async Task ReadAsync_NonSeekableStream_IsAccepted()
    {
        byte[] pdf = PdfLike(5000);
        using var stream = new TrickleStream(pdf, 7);

        PdfInput input = await PdfInputReader.ReadAsync(stream, null, TestContext.Current.CancellationToken);

        Assert.Equal(pdf, input.Bytes);
    }

    [Fact]
    public async Task ReadAsync_DoesNotDisposeCallerStream()
    {
        using var stream = new MemoryStream(PdfLike(50));
        await PdfInputReader.ReadAsync(stream, null, TestContext.Current.CancellationToken);
        Assert.True(stream.CanRead);
        Assert.Equal(50, stream.Length); // would throw ObjectDisposedException when disposed
    }

    [Fact]
    public async Task ReadAsync_ExceedingLimit_ThrowsAndStopsReadingAtLimitPlusOne()
    {
        using var stream = new TrickleStream(PdfLike(1000), 64);
        PdfLimitExceededException ex = await Assert.ThrowsAsync<PdfLimitExceededException>(
            () => PdfInputReader.ReadAsync(stream, 100, TestContext.Current.CancellationToken));

        Assert.Equal(PdfLimit.InputSize, ex.Limit);
        Assert.Equal(100, ex.Configured);
        Assert.Equal(101, ex.Measured);
        Assert.True(stream.TotalBytesRead <= 101, $"Read {stream.TotalBytesRead} bytes");
    }

    [Fact]
    public async Task ReadAsync_ExactlyAtLimit_Succeeds()
    {
        using var stream = new MemoryStream(PdfLike(100));
        PdfInput input = await PdfInputReader.ReadAsync(stream, 100, TestContext.Current.CancellationToken);
        Assert.Equal(100, input.ByteLength);
    }

    [Fact]
    public async Task ReadAsync_NullLimit_DisablesLimit()
    {
        using var stream = new MemoryStream(PdfLike(300_000));
        PdfInput input = await PdfInputReader.ReadAsync(stream, null, TestContext.Current.CancellationToken);
        Assert.Equal(300_000, input.ByteLength);
    }

    [Fact]
    public async Task ReadAsync_ComputesLowercaseHexSha256()
    {
        byte[] pdf = PdfLike(777);
        using var stream = new MemoryStream(pdf);

        PdfInput input = await PdfInputReader.ReadAsync(stream, null, TestContext.Current.CancellationToken);

        string expected = Convert.ToHexStringLower(SHA256.HashData(pdf));
        Assert.Equal(expected, input.Sha256);
        Assert.Equal(64, input.Sha256.Length);
    }

    [Fact]
    public async Task ReadAsync_UnreadableStream_ThrowsArgumentException()
    {
        using var stream = new WriteOnlyStream();
        await Assert.ThrowsAsync<ArgumentException>(
            () => PdfInputReader.ReadAsync(stream, null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ReadAsync_NullStream_ThrowsArgumentNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => PdfInputReader.ReadAsync(null!, null, TestContext.Current.CancellationToken));
    }

    /// <summary>Non-seekable read-only stream returning at most <c>chunk</c> bytes per read.</summary>
    private sealed class TrickleStream(byte[] data, int chunk) : Stream
    {
        private readonly MemoryStream _inner = new(data);

        public long TotalBytesRead { get; private set; }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int n = _inner.Read(buffer, offset, Math.Min(count, chunk));
            TotalBytesRead += n;
            return n;
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class WriteOnlyStream : Stream
    {
        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
        {
        }
    }
}
