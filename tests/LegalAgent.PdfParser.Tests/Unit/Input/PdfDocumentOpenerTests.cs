using System.Text;
using LegalAgent.PdfParser.Input;
using LegalAgent.PdfParser.Tests.Fixtures;

namespace LegalAgent.PdfParser.Tests.Unit.Input;

public sealed class PdfDocumentOpenerTests
{
    private static byte[] ErrorFixture(string name) =>
        File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Corpus", "errors", name));

    private static PdfInput InputOf(byte[] bytes) =>
        new(bytes, bytes.Length, new string('0', 64));

    [Fact]
    public void Open_GarbageBodyAfterHeader_ThrowsCorrupted()
    {
        byte[] bytes = Encoding.ASCII.GetBytes("%PDF-1.7\nthis is definitely not a pdf structure\n%%EOF\n");

        InvalidPdfException ex = Assert.Throws<InvalidPdfException>(
            () => PdfDocumentOpener.Open(InputOf(bytes), null, new LimitsOptions()));

        Assert.Equal(InvalidPdfReason.Corrupted, ex.Reason);
    }

    [Fact]
    public void Open_TruncatedFixture_ThrowsCorrupted()
    {
        InvalidPdfException ex = Assert.Throws<InvalidPdfException>(
            () => PdfDocumentOpener.Open(InputOf(ErrorFixture("truncated.pdf")), null, new LimitsOptions()));

        Assert.Equal(InvalidPdfReason.Corrupted, ex.Reason);
    }

    [Fact]
    public void Open_EncryptedFixture_ThrowsPdfEncrypted()
    {
        Assert.Throws<PdfEncryptedException>(
            () => PdfDocumentOpener.Open(InputOf(ErrorFixture("encrypted.pdf")), null, new LimitsOptions()));
    }

    [Fact]
    public void Open_PermissionsOnlyFixture_OpensAndReadsTextNormally()
    {
        using OpenedPdf opened = PdfDocumentOpener.Open(InputOf(ErrorFixture("permissions-only.pdf")), null, new LimitsOptions());

        Assert.Equal(3, opened.Source.PageCount);
        string text = string.Concat(opened.Document.GetPage(1).Letters.Select(l => l.Value));
        Assert.Contains("Przykladowy", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Open_PageCountAboveLimit_ThrowsPageCountLimit()
    {
        byte[] bytes = new SyntheticPdfBuilder()
            .Page().Text(50, 100, "a")
            .Page().Text(50, 100, "b")
            .Page().Text(50, 100, "c")
            .Build();

        PdfLimitExceededException ex = Assert.Throws<PdfLimitExceededException>(
            () => PdfDocumentOpener.Open(InputOf(bytes), null, new LimitsOptions { MaxPages = 2 }));

        Assert.Equal(PdfLimit.PageCount, ex.Limit);
        Assert.Equal(2, ex.Configured);
        Assert.Equal(3, ex.Measured);
    }

    [Fact]
    public void Open_PageCountEqualToLimit_IsAccepted()
    {
        byte[] bytes = new SyntheticPdfBuilder().Page().Text(50, 100, "a").Page().Text(50, 100, "b").Build();

        using OpenedPdf opened = PdfDocumentOpener.Open(InputOf(bytes), null, new LimitsOptions { MaxPages = 2 });

        Assert.Equal(2, opened.Source.PageCount);
    }

    [Fact]
    public void Open_NullPageLimit_DisablesLimit()
    {
        byte[] bytes = new SyntheticPdfBuilder().Page().Text(50, 100, "a").Page().Text(50, 100, "b").Build();

        using OpenedPdf opened = PdfDocumentOpener.Open(InputOf(bytes), null, new LimitsOptions { MaxPages = null });

        Assert.Equal(2, opened.Source.PageCount);
    }

    [Fact]
    public void Open_PopulatesSourceInfo()
    {
        byte[] bytes = new SyntheticPdfBuilder()
            .Title("Regulamin rachunku")
            .Page().Text(50, 100, "a")
            .Build();
        var input = new PdfInput(bytes, bytes.Length, "abc123");

        using OpenedPdf opened = PdfDocumentOpener.Open(input, "regulamin.pdf", new LimitsOptions());

        Assert.Equal("regulamin.pdf", opened.Source.SourceId);
        Assert.Equal(1, opened.Source.PageCount);
        Assert.Equal("Regulamin rachunku", opened.Source.PdfTitle);
        Assert.Equal(bytes.Length, opened.Source.ByteLength);
        Assert.Equal("abc123", opened.Source.Sha256);
    }

    [Fact]
    public void Open_NoTitle_PdfTitleIsNull()
    {
        byte[] bytes = new SyntheticPdfBuilder().Page().Text(50, 100, "a").Build();

        using OpenedPdf opened = PdfDocumentOpener.Open(InputOf(bytes), null, new LimitsOptions());

        Assert.Null(opened.Source.PdfTitle);
    }
}
