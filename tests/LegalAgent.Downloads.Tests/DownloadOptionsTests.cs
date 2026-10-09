namespace LegalAgent.Downloads.Tests;

public sealed class DownloadOptionsTests
{
    private static readonly DownloadOptions Valid = new() { AllowedHosts = ["example.test"] };

    [Fact]
    public void Defaults_MatchDataModel()
    {
        var options = new DownloadOptions();

        Assert.Equal("downloads", options.OutputDirectory);
        Assert.Empty(options.AllowedHosts);
        Assert.False(options.AllowHttp);
        Assert.Equal(TimeSpan.FromSeconds(60), options.Timeout);
        Assert.Equal(50L * 1024 * 1024, options.MaxFileSizeBytes);
        Assert.Equal(5, options.MaxRedirects);
        Assert.Null(options.UserAgent);
    }

    [Fact]
    public void Constructor_ValidOptions_DoesNotThrow()
    {
        using var client = new HttpClient();
        _ = new DocumentDownloader(client, Valid);
    }

    public static TheoryData<DownloadOptions> InvalidOptions() =>
    [
        Valid with { OutputDirectory = "" },
        Valid with { OutputDirectory = "   " },
        Valid with { AllowedHosts = [] },
        Valid with { AllowedHosts = ["https://mbank.pl"] },
        Valid with { AllowedHosts = ["mbank.pl:443"] },
        Valid with { AllowedHosts = ["mbank.pl/x"] },
        Valid with { AllowedHosts = [""] },
        Valid with { Timeout = TimeSpan.Zero },
        Valid with { Timeout = TimeSpan.FromSeconds(-1) },
        Valid with { MaxFileSizeBytes = 0 },
        Valid with { MaxRedirects = -1 },
        Valid with { MaxRedirects = 21 },
    ];

    [Theory]
    [MemberData(nameof(InvalidOptions))]
    public void Constructor_InvalidOptions_ThrowsArgumentException(DownloadOptions options)
    {
        using var client = new HttpClient();
        Assert.ThrowsAny<ArgumentException>(() => new DocumentDownloader(client, options));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(20)]
    public void Constructor_RedirectLimitAtBounds_IsAccepted(int maxRedirects)
    {
        using var client = new HttpClient();
        _ = new DocumentDownloader(client, Valid with { MaxRedirects = maxRedirects });
    }
}
