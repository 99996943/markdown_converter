using LegalAgent.PdfParser.Text;

namespace LegalAgent.PdfParser.Tests.Unit.Text;

public sealed class HyphenationTests
{
    private static readonly string[] Exceptions = ["e-mail", "biało-czerwony"];

    [Theory]
    [InlineData("umowa z przedsiębior-", "ca zawarta", HyphenJoin.Remove)]
    [InlineData("(przedsiębior-", "ca)", HyphenJoin.Remove)]
    [InlineData("Przedsiębior-", "ca zawarta", HyphenJoin.Remove)]
    [InlineData("wysyłka na adres e-", "mail klienta", HyphenJoin.Keep)]
    [InlineData("flaga biało-", "czerwony wiatr", HyphenJoin.Keep)]
    [InlineData("miasto Bielsko-", "Biała", HyphenJoin.Keep)]
    [InlineData("w mieście Bielsko-", "biała", HyphenJoin.Remove)]
    [InlineData("Ministra Spraw Zagra-", "nicznych (Dz. U.", HyphenJoin.Remove)]
    [InlineData("rozporządzenia Prezesa Rady Mini-", "strów z dnia", HyphenJoin.Remove)]
    [InlineData("zgodnie z PKB-", "owskim wskaźnikiem", HyphenJoin.Keep)]
    [InlineData("opłata 10-", "dniowa", HyphenJoin.None)]
    [InlineData("kwota netto -", "brutto", HyphenJoin.None)]
    [InlineData("koniec zdania", "dalej", HyphenJoin.None)]
    [InlineData("rachunek-", "123", HyphenJoin.None)]
    [InlineData("tekst-", "", HyphenJoin.None)]
    [InlineData("", "ca", HyphenJoin.None)]
    public void Decide_FollowsFr012(string lineEnd, string nextLineStart, HyphenJoin expected)
    {
        Assert.Equal(expected, Hyphenation.Decide(lineEnd, nextLineStart, Exceptions));
    }

    [Fact]
    public void Decide_ExceptionsAreCaseInsensitiveAndConfigurable()
    {
        Assert.Equal(HyphenJoin.Keep, Hyphenation.Decide("adres E-", "Mail", Exceptions));
        Assert.Equal(HyphenJoin.Keep, Hyphenation.Decide("sprzedaż on-", "line", ["on-line"]));
        Assert.Equal(HyphenJoin.Remove, Hyphenation.Decide("sprzedaż on-", "line", []));
    }

    [Fact]
    public void Decide_ExceptionMatchIgnoresPunctuationAfterNextWord()
    {
        Assert.Equal(HyphenJoin.Keep, Hyphenation.Decide("flaga biało-", "czerwony, a", Exceptions));
    }

    [Theory]
    [InlineData("więcej na mbank.pl/pierscien-platniczy-", "mastercard", HyphenJoin.Keep)]
    [InlineData("strona www.example.org/a-", "b", HyphenJoin.Keep)]
    [InlineData("zobacz https://x.pl/rozwijaj-", "firme", HyphenJoin.Keep)]
    [InlineData("zobacz www.przyklad-", "bank.pl", HyphenJoin.Keep)]
    [InlineData("https://example.org/pdf/promocja-01-09-26-", "30-11-26.pdf", HyphenJoin.Keep)]
    [InlineData("Ministra Spraw Zagra-", "nicznych", HyphenJoin.Remove)]
    public void Decide_KeepsHyphenInWebAddress(string lineEnd, string nextLineStart, HyphenJoin expected)
    {
        Assert.Equal(expected, Hyphenation.Decide(lineEnd, nextLineStart, Exceptions));
    }
}
