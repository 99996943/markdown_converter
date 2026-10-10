using System.Text.Json;
using MBank.FaqGenerator.Tests.Fakes;

namespace MBank.FaqGenerator.Tests;

public sealed class AddressSourceTests : IDisposable
{
    private static readonly string[] Urls = [.. Enumerable.Range(1, 6).Select(i => $"https://www.example.test/pdf/reg-{i}.pdf")];

    private readonly AppHarness app = new();

    public AddressSourceTests()
    {
        app.ServeRegulations(Urls);
    }

    public void Dispose() => app.Dispose();

    [Fact]
    public async Task FiveUrlArguments_DownloadWithoutPrompts()
    {
        AppRun run = await app.RunAsync(UrlArgs(Urls[..5]), "", Ct);

        Assert.Equal(0, run.Code);
        Assert.DoesNotContain("Podaj adres", run.Out, StringComparison.Ordinal);
        Assert.Equal(Urls[..5].Order(StringComparer.Ordinal), RequestedUrls());
    }

    [Fact]
    public async Task FiveUrlsInConfiguration_DownloadWithoutPrompts()
    {
        SetConfigUrls(Urls[1..6]);

        AppRun run = await app.RunAsync([], "", Ct);

        Assert.Equal(0, run.Code);
        Assert.DoesNotContain("Podaj adres", run.Out, StringComparison.Ordinal);
        Assert.Equal(Urls[1..6].Order(StringComparer.Ordinal), RequestedUrls());
    }

    [Fact]
    public async Task ArgumentsTakePrecedenceOverConfiguration()
    {
        SetConfigUrls(Urls[1..6]);

        AppRun run = await app.RunAsync(UrlArgs(Urls[..5]), "", Ct);

        Assert.Equal(0, run.Code);
        Assert.Equal(Urls[..5].Order(StringComparer.Ordinal), RequestedUrls());
    }

    [Fact]
    public async Task EmptyConfigurationList_MeansPrompts()
    {
        AppRun run = await app.RunAsync([], string.Join("\n", Urls[..5]) + "\n", Ct);

        Assert.Equal(0, run.Code);
        Assert.Contains("Podaj adres regulaminu 5 z 5: ", run.Out, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(6)]
    public async Task WrongNumberOfUrlArguments_ExitCode2_WithoutDownloads(int count)
    {
        AppRun run = await app.RunAsync(UrlArgs(Urls[..count]), "", Ct);

        Assert.Equal(2, run.Code);
        Assert.Contains("5", run.Err, StringComparison.Ordinal);
        AssertNothingDownloaded();
    }

    [Fact]
    public async Task ThreeUrlsInConfiguration_ExitCode2()
    {
        SetConfigUrls(Urls[..3]);

        AppRun run = await app.RunAsync([], "", Ct);

        Assert.Equal(2, run.Code);
        AssertNothingDownloaded();
    }

    [Theory]
    [InlineData("https://www.mbank.pl/a.pdf")]
    [InlineData("abc")]
    [InlineData("https://www.example.test/pdf/reg-1.pdf")]
    public async Task InvalidOrDuplicateAddressAtPosition4_ExitCode2_NamesPosition(string fourth)
    {
        string[] urls = [Urls[0], Urls[1], Urls[2], fourth, Urls[4]];

        AppRun run = await app.RunAsync(UrlArgs(urls), "", Ct);

        Assert.Equal(2, run.Code);
        Assert.Contains("Adres 4:", run.Err, StringComparison.Ordinal);
        AssertNothingDownloaded();
    }

    [Fact]
    public async Task OutputOption_OverridesConfiguredDirectory()
    {
        string other = app.Root.Combine("inny");

        AppRun run = await app.RunAsync([.. UrlArgs(Urls[..5]), "--output", other], "", Ct);

        Assert.Equal(0, run.Code);
        Assert.True(File.Exists(Path.Combine(other, "reg-1.pdf")));
        Assert.False(Directory.Exists(app.OutputDirectory));
    }

    [Theory]
    [InlineData("--nieznana")]
    [InlineData("--url")]
    [InlineData("--output")]
    [InlineData("plik.pdf")]
    public async Task BadArguments_ExitCode2(string argument)
    {
        AppRun run = await app.RunAsync([argument], "", Ct);

        Assert.Equal(2, run.Code);
        Assert.Contains("Użycie", run.Err, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    public async Task Help_ExitCode0(string argument)
    {
        AppRun run = await app.RunAsync([argument], "", Ct);

        Assert.Equal(0, run.Code);
        Assert.Contains("Użycie", run.Out, StringComparison.Ordinal);
        Assert.Contains("--url", run.Out, StringComparison.Ordinal);
        Assert.Empty(app.Http.Requests);
    }

    [Fact]
    public async Task Version_ExitCode0()
    {
        AppRun run = await app.RunAsync(["--version"], "", Ct);

        Assert.Equal(0, run.Code);
        Assert.Matches(@"^mBank\.FaqGenerator \d+\.\d+\.\d+", run.Out);
    }

    [Fact]
    public void ShippedSettings_AllowMBankOnly_AndHaveNoUrls()
    {
        using JsonDocument settings = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "appsettings.json")));
        JsonElement download = settings.RootElement.GetProperty("Download");

        Assert.Equal(["mbank.pl"], download.GetProperty("AllowedHosts").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal(0, download.GetProperty("Urls").GetArrayLength());
        Assert.Equal("downloads", download.GetProperty("OutputDirectory").GetString());
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string[] UrlArgs(IEnumerable<string> urls) => [.. urls.SelectMany(u => new[] { "--url", u })];

    private void SetConfigUrls(string[] urls)
    {
        for (int i = 0; i < urls.Length; i++)
        {
            app.Environment[$"FAQGEN__Download__Urls__{i}"] = urls[i];
        }
    }

    private List<string> RequestedUrls() =>
        [.. app.Http.Requests.Select(r => r.Address.AbsoluteUri).Order(StringComparer.Ordinal)];

    private void AssertNothingDownloaded()
    {
        Assert.Empty(app.Http.Requests);
        Assert.False(Directory.Exists(app.OutputDirectory));
    }
}
