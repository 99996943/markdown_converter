namespace LegalAgent.Downloads.Tests;

public sealed class HostAllowListTests
{
    private static readonly HostAllowList MBank = new(["mbank.pl"]);

    [Theory]
    [InlineData("https://mbank.pl/a.pdf")]
    [InlineData("https://www.mbank.pl/a.pdf")]
    [InlineData("https://WWW.MBANK.PL/a.pdf")]
    [InlineData("https://a.b.mbank.pl/a.pdf")]
    [InlineData("https://www.mbank.pl./a.pdf")]
    public void IsAllowed_HostOrSubdomain_True(string url) =>
        Assert.True(MBank.IsAllowed(new Uri(url)));

    [Theory]
    [InlineData("https://mbank.pl.evil.com/a.pdf")]
    [InlineData("https://evilmbank.pl/a.pdf")]
    [InlineData("https://mbank.com/a.pdf")]
    [InlineData("https://192.168.0.1/a.pdf")]
    [InlineData("https://[::1]/a.pdf")]
    public void IsAllowed_OtherHost_False(string url) =>
        Assert.False(MBank.IsAllowed(new Uri(url)));

    [Fact]
    public void IsAllowed_UnicodeHost_ComparedInPunycode()
    {
        string punycode = new System.Globalization.IdnMapping().GetAscii("żółw.pl");
        var unicodeEntry = new HostAllowList(["żółw.pl"]);
        var punycodeEntry = new HostAllowList([punycode]);

        Assert.True(unicodeEntry.IsAllowed(new Uri($"https://www.{punycode}/a.pdf")));
        Assert.True(unicodeEntry.IsAllowed(new Uri("https://żółw.pl/a.pdf")));
        Assert.True(punycodeEntry.IsAllowed(new Uri("https://www.żółw.pl/a.pdf")));
        Assert.False(punycodeEntry.IsAllowed(new Uri("https://zolw.pl/a.pdf")));
    }
}
