using System.Text;
using System.Text.RegularExpressions;
using LegalAgent.Corpus.Pdf;
using UglyToad.PdfPig;

namespace LegalAgent.Corpus.Tests.Unit.Pdf;

public sealed class PdfIdNormalizerTests
{
    private static readonly Regex IdPattern = new(@"/ID \[ <([0-9A-Fa-f]{32})><([0-9A-Fa-f]{32})>\]", RegexOptions.CultureInvariant);

    private static byte[] Doc(int pages, string text = "Zażółć gęślą jaźń")
    {
        var b = new SyntheticPdfBuilder();
        for (int i = 0; i < pages; i++)
        {
            b.Page();
            for (int line = 0; line < 20; line++)
            {
                b.Text(50, 80 + (line * 14), text + " " + i + "/" + line);
            }
        }

        return b.Build();
    }

    private static string Latin1(byte[] bytes) => Encoding.Latin1.GetString(bytes);

    [Theory]
    [InlineData(1)]
    [InlineData(25)]
    public void Build_SameDocumentTwice_ProducesIdenticalBytes(int pages)
    {
        Assert.Equal(Doc(pages), Doc(pages));
    }

    [Fact]
    public void Build_KeepsIdFormatAndLength()
    {
        byte[] pdf = Doc(1);
        MatchCollection matches = IdPattern.Matches(Latin1(pdf));
        Assert.Single(matches);
    }

    [Fact]
    public void Normalize_ReplacesIdInPlaceWithoutChangingLength()
    {
        byte[] raw = Doc(1);
        byte[] normalized = PdfIdNormalizer.Normalize(raw);
        Assert.Equal(raw.Length, normalized.Length);
        Assert.Matches(IdPattern, Latin1(normalized));
    }

    [Fact]
    public void Normalize_WithoutId_ReturnsInputUnchanged()
    {
        byte[] data = Encoding.ASCII.GetBytes("%PDF-1.7\ntrailer\n<</Size 1>>\n%%EOF");
        Assert.Equal(data, PdfIdNormalizer.Normalize(data));
    }

    [Fact]
    public void Build_DifferentContent_ProducesDifferentId()
    {
        string a = IdPattern.Match(Latin1(Doc(1, "alfa"))).Value;
        string b = IdPattern.Match(Latin1(Doc(1, "beta"))).Value;
        Assert.NotEqual(a, b);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(25)]
    public void Build_Result_OpensStrictlyWithSamePageCount(int pages)
    {
        using PdfDocument doc = PdfDocument.Open(Doc(pages), new ParsingOptions { UseLenientParsing = false });
        Assert.Equal(pages, doc.NumberOfPages);
    }
}
