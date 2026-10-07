using LegalAgent.PdfParser.Text;

namespace LegalAgent.PdfParser.Tests.Unit.Text;

public sealed class PageNumberPatternsTests
{
    [Theory]
    [InlineData("3", 3)]
    [InlineData("12", 12)]
    [InlineData("iv", 4)]
    [InlineData("XII", 12)]
    [InlineData("- 3 -", 3)]
    [InlineData("– 3 –", 3)]
    [InlineData("—7—", 7)]
    [InlineData("3 / 40", 3)]
    [InlineData("3/40", 3)]
    [InlineData("Strona 3 z 40", 3)]
    [InlineData("strona 3", 3)]
    [InlineData("Str. 3", 3)]
    [InlineData("s. 3/40", 3)]
    [InlineData("S. 3 / 40", 3)]
    public void TryParse_RecognisesPageNumberFormats(string text, int expected)
    {
        Assert.True(PageNumberPatterns.TryParse(text, out int number));
        Assert.Equal(expected, number);
    }

    [Theory]
    [InlineData("3 zł")]
    [InlineData("art. 3")]
    [InlineData("2024")]
    [InlineData("Poz. 1234")]
    [InlineData("1)")]
    [InlineData("§ 3")]
    [InlineData("")]
    [InlineData("Dziennik Ustaw – 3 – Poz. 1234")]
    public void TryParse_RejectsNonPageNumbers(string text)
    {
        Assert.False(PageNumberPatterns.TryParse(text, out _));
    }

    [Fact]
    public void DetectOffset_ReturnsModeOfPrintedMinusPhysical()
    {
        (int Physical, int Printed)[] observations =
        [
            (1, 3), (2, 4), (3, 5), (4, 6),
            (5, 1), // stray number that does not follow the sequence
        ];

        Assert.Equal(2, PageNumberPatterns.DetectOffset(observations));
    }

    [Fact]
    public void DetectOffset_TieIsBrokenBySmallestAbsoluteOffset()
    {
        (int Physical, int Printed)[] observations = [(1, 1), (2, 5)];

        Assert.Equal(0, PageNumberPatterns.DetectOffset(observations));
    }

    [Fact]
    public void DetectOffset_NoObservations_ReturnsNull()
    {
        Assert.Null(PageNumberPatterns.DetectOffset([]));
    }
}
