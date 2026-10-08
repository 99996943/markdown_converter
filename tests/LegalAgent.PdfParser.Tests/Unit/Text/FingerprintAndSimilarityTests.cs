using LegalAgent.PdfParser.Text;

namespace LegalAgent.PdfParser.Tests.Unit.Text;

public sealed class FingerprintAndSimilarityTests
{
    [Fact]
    public void Fingerprint_ReplacesDigitRunsWithHash()
    {
        Assert.Equal(
            LineFingerprint.Compute("Dziennik Ustaw – 3 – Poz. 1234"),
            LineFingerprint.Compute("Dziennik Ustaw – 17 – Poz. 1234"));
        Assert.Equal("dziennik ustaw – # – poz. #", LineFingerprint.Compute("Dziennik Ustaw – 3 – Poz. 1234"));
    }

    [Fact]
    public void Fingerprint_LowercasesInvariantlyAndKeepsPolishLetters()
    {
        Assert.Equal("ustawa o zażółceniu", LineFingerprint.Compute("USTAWA O ZAŻÓŁCENIU"));
    }

    [Fact]
    public void Fingerprint_CollapsesWhitespace()
    {
        Assert.Equal("strona # z #", LineFingerprint.Compute("  Strona\t3   z 40 "));
    }

    [Theory]
    [InlineData("- 3 -", "#")]
    [InlineData("– 12 –", "#")]
    [InlineData("...Regulamin konta.", "regulamin konta")]
    [InlineData("(3)", "#")]
    public void Fingerprint_TrimsEdgePunctuation(string input, string expected)
    {
        Assert.Equal(expected, LineFingerprint.Compute(input));
    }

    [Fact]
    public void Fingerprint_OfPunctuationOnlyLine_IsEmpty()
    {
        Assert.Equal(string.Empty, LineFingerprint.Compute(" ___ * * * "));
    }

    [Theory]
    [InlineData("kitten", "sitting", 3)]
    [InlineData("", "abc", 3)]
    [InlineData("abc", "abc", 0)]
    [InlineData("rozdział", "rozdzial", 1)]
    public void Levenshtein_Distance(string a, string b, int expected)
    {
        Assert.Equal(expected, Levenshtein.Distance(a, b));
    }

    [Fact]
    public void Levenshtein_Similarity_IsNormalisedByLongerString()
    {
        Assert.Equal(1.0, Levenshtein.Similarity("", ""));
        Assert.Equal(1.0, Levenshtein.Similarity("abc", "abc"));
        Assert.Equal(1.0 - (3.0 / 7.0), Levenshtein.Similarity("kitten", "sitting"), 10);
    }

    [Fact]
    public void Levenshtein_Similarity_ThresholdBehaviour()
    {
        // Running header differing only in the current chapter name stays above 0.85.
        string a = LineFingerprint.Compute("Regulamin rachunków osobistych – Postanowienia ogólne");
        string b = LineFingerprint.Compute("Regulamin rachunków osobistych – Postanowienia końcowe");
        Assert.True(Levenshtein.Similarity(a, b) >= 0.85);

        // Unrelated lines fall well below.
        Assert.True(Levenshtein.Similarity("dziennik ustaw – # – poz. #", "opłata za prowadzenie rachunku") < 0.85);
    }
}
