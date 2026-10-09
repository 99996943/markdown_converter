using LegalAgent.PdfParser.Text;

namespace LegalAgent.PdfParser.Tests.Unit.Text;

/// <summary>R10 — font family of a PDF font name.</summary>
public sealed class FontFamilyTests
{
    [Theory]
    [InlineData("Verdana", "Verdana")]
    [InlineData("ABCDEF+Verdana-Bold", "Verdana")]
    [InlineData("CourierNewPSMT", "CourierNewPSMT")]
    [InlineData("Arial,Bold", "Arial")]
    [InlineData("ABCDEF+Arial,BoldItalic", "Arial")]
    [InlineData("AbCDEF+Verdana", "AbCDEF+Verdana")]
    [InlineData("ABCDE+Verdana", "ABCDE+Verdana")]
    public void Of_StripsSubsetPrefixAndStyleSuffix(string fontName, string expected)
    {
        Assert.Equal(expected, FontFamily.Of(fontName));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Of_ReturnsNull_WithoutAName(string? fontName)
    {
        Assert.Null(FontFamily.Of(fontName));
    }
}
