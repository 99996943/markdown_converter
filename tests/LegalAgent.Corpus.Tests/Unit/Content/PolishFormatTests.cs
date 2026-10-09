using LegalAgent.Corpus.Content;

namespace LegalAgent.Corpus.Tests.Unit.Content;

public sealed class PolishFormatTests
{
    [Theory]
    [InlineData(25, "25,00 zł")]
    [InlineData(1234.5, "1 234,50 zł")]
    [InlineData(0, "0,00 zł")]
    [InlineData(1000000, "1 000 000,00 zł")]
    public void Amount_FormatsWithCommaAndSpaces(double value, string expected) =>
        Assert.Equal(expected, PolishFormat.Amount((decimal)value));

    [Fact]
    public void Amount_UsesNormalSpace()
    {
        string s = PolishFormat.Amount(1234.5m);
        Assert.Contains(" ", s, StringComparison.Ordinal);
        Assert.DoesNotContain((char)0xA0, s);
    }

    [Fact]
    public void Amount_Negative_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => PolishFormat.Amount(-1m));

    [Theory]
    [InlineData(1.5, "1,5%")]
    [InlineData(2, "2%")]
    [InlineData(0.25, "0,25%")]
    [InlineData(12.50, "12,5%")]
    public void Percent_NoTrailingZeros(double value, string expected) =>
        Assert.Equal(expected, PolishFormat.Percent((decimal)value));

    [Theory]
    [InlineData(1, "1 dzień")]
    [InlineData(2, "2 dni")]
    [InlineData(5, "5 dni")]
    [InlineData(14, "14 dni")]
    [InlineData(22, "22 dni")]
    [InlineData(0, "0 dni")]
    public void Days_Forms(int value, string expected) =>
        Assert.Equal(expected, PolishFormat.Days(value));

    [Theory]
    [InlineData(2027, 1, 1, "1 stycznia 2027 r.")]
    [InlineData(2026, 9, 15, "15 września 2026 r.")]
    [InlineData(2026, 10, 8, "8 października 2026 r.")]
    public void Date_GenitiveMonth(int y, int m, int d, string expected) =>
        Assert.Equal(expected, PolishFormat.Date(new DateOnly(y, m, d)));

    [Fact]
    public void Format_MissingValue_Throws()
    {
        Assert.Throws<ContentException>(() => PolishFormat.Format(FactKind.Kwota, new FactValue()));
        Assert.Throws<ContentException>(() => PolishFormat.Format(FactKind.Tekst, new FactValue()));
        Assert.Throws<ContentException>(() => PolishFormat.Format(FactKind.Data, new FactValue()));
    }

    [Fact]
    public void Format_DispatchesByKind()
    {
        Assert.Equal("25,00 zł", PolishFormat.Format(FactKind.Kwota, new FactValue(Number: 25m)));
        Assert.Equal("1,5%", PolishFormat.Format(FactKind.Procent, new FactValue(Number: 1.5m)));
        Assert.Equal("14 dni", PolishFormat.Format(FactKind.Termin, new FactValue(Number: 14m)));
        Assert.Equal("Biuro", PolishFormat.Format(FactKind.Tekst, new FactValue(Text: "Biuro")));
        Assert.Equal("1 stycznia 2027 r.", PolishFormat.Format(FactKind.Data, new FactValue(Date: new DateOnly(2027, 1, 1))));
    }
}
