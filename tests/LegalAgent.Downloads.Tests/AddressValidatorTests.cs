namespace LegalAgent.Downloads.Tests;

public sealed class AddressValidatorTests
{
    private static readonly DownloadOptions Options = new() { AllowedHosts = ["mbank.pl"] };

    private static AddressCheck Check(string? input, params Uri[] previous) =>
        AddressValidator.Check(input, previous, Options);

    [Fact]
    public void Check_ValidAddress_IsTrimmedAndAccepted()
    {
        AddressCheck check = Check("  https://www.mbank.pl/pdf/reg.pdf \t");

        Assert.True(check.IsValid);
        Assert.Equal(new Uri("https://www.mbank.pl/pdf/reg.pdf"), check.Address);
        Assert.Null(check.Error);
        Assert.Null(check.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Check_Empty_Rejected(string? input) => AssertRejected(Check(input), AddressError.Empty);

    [Theory]
    [InlineData("abc")]
    [InlineData("/pdf/a.pdf")]
    [InlineData("www.mbank.pl/a.pdf")]
    public void Check_NotAbsolute_Rejected(string input) => AssertRejected(Check(input), AddressError.NotAbsolute);

    [Theory]
    [InlineData("ftp://mbank.pl/a.pdf")]
    [InlineData("http://www.mbank.pl/a.pdf")]
    [InlineData("file:///C:/a.pdf")]
    public void Check_SchemeNotAllowed_Rejected(string input) => AssertRejected(Check(input), AddressError.SchemeNotAllowed);

    [Fact]
    public void Check_Http_AcceptedWhenAllowed()
    {
        AddressCheck check = AddressValidator.Check("http://www.mbank.pl/a.pdf", [], Options with { AllowHttp = true });

        Assert.True(check.IsValid);
    }

    [Theory]
    [InlineData("https://mbank.pl@evil.com/a.pdf")]
    [InlineData("https://u:p@www.mbank.pl/a.pdf")]
    public void Check_UserInfo_Rejected(string input) => AssertRejected(Check(input), AddressError.HasUserInfo);

    [Fact]
    public void Check_HostNotAllowed_MessageNamesHostAndList()
    {
        AddressCheck check = Check("https://example.com/a.pdf");

        AssertRejected(check, AddressError.HostNotAllowed);
        Assert.Contains("example.com", check.Message, StringComparison.Ordinal);
        Assert.Contains("mbank.pl", check.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("https://www.mbank.pl/a.pdf")]
    [InlineData("https://WWW.MBANK.PL/a.pdf")]
    [InlineData("https://www.mbank.pl/a.pdf#page=2")]
    [InlineData(" https://www.mbank.pl/a.pdf ")]
    public void Check_Duplicate_Rejected(string input) =>
        AssertRejected(Check(input, new Uri("https://www.mbank.pl/a.pdf")), AddressError.Duplicate);

    [Fact]
    public void Check_DifferentQuery_IsNotDuplicate() =>
        Assert.True(Check("https://www.mbank.pl/a.pdf?v=2", new Uri("https://www.mbank.pl/a.pdf")).IsValid);

    [Fact]
    public void Check_MessagesArePolish()
    {
        Assert.Contains("adres", Check("abc").Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("https", Check("ftp://mbank.pl/a.pdf").Message, StringComparison.Ordinal);
        Assert.Contains("powt", Check("https://mbank.pl/a.pdf", new Uri("https://mbank.pl/a.pdf")).Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CheckAll_ReturnsResultsInOrder_AndDetectsDuplicatesOfEarlierPositions()
    {
        IReadOnlyList<AddressCheck> checks = AddressValidator.CheckAll(
            [
                "https://mbank.pl/1.pdf",
                "abc",
                "https://mbank.pl/2.pdf",
                "https://mbank.pl/1.pdf",
                "https://example.com/3.pdf",
            ],
            Options);

        Assert.Equal(5, checks.Count);
        Assert.True(checks[0].IsValid);
        Assert.Equal(AddressError.NotAbsolute, checks[1].Error);
        Assert.True(checks[2].IsValid);
        Assert.Equal(AddressError.Duplicate, checks[3].Error);
        Assert.Equal(AddressError.HostNotAllowed, checks[4].Error);
    }

    private static void AssertRejected(AddressCheck check, AddressError expected)
    {
        Assert.False(check.IsValid);
        Assert.Null(check.Address);
        Assert.Equal(expected, check.Error);
        Assert.False(string.IsNullOrWhiteSpace(check.Message));
    }
}
