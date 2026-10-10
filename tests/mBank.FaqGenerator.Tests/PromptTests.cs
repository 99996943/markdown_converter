using System.Text.RegularExpressions;
using MBank.FaqGenerator.Tests.Fakes;

namespace MBank.FaqGenerator.Tests;

public sealed partial class PromptTests : IDisposable
{
    private static readonly string[] Urls = [.. Enumerable.Range(1, 5).Select(i => $"https://www.example.test/pdf/reg-{i}.pdf")];

    private readonly AppHarness app = new();

    public PromptTests()
    {
        foreach (string url in Urls)
        {
            app.Http.Pdf(url);
        }
    }

    public void Dispose() => app.Dispose();

    [Fact]
    public async Task NoArguments_PromptsForFiveAddresses_AndDownloads()
    {
        AppRun run = await app.RunAsync([], Lines(Urls), Ct);

        Assert.Equal(0, run.Code);
        for (int n = 1; n <= 5; n++)
        {
            Assert.Contains($"Podaj adres regulaminu {n} z 5: ", run.Out, StringComparison.Ordinal);
        }

        Assert.Equal(5, Prompts(run.Out).Count);
        Assert.Equal(
            ["manifest.json", "reg-1.pdf", "reg-2.pdf", "reg-3.pdf", "reg-4.pdf", "reg-5.pdf"],
            app.Root.FileNames("downloads"));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("ftp://www.example.test/a.pdf")]
    [InlineData("https://www.mbank.pl/a.pdf")]
    public async Task InvalidAddress_IsExplained_AndSameNumberIsAskedAgain(string invalid)
    {
        AppRun run = await app.RunAsync([], Lines([Urls[0], invalid, .. Urls[1..]]), Ct);

        Assert.Equal(0, run.Code);
        Assert.Contains("Niepoprawny adres: ", run.Out, StringComparison.Ordinal);
        Assert.Equal(["1", "2", "2", "3", "4", "5"], Prompts(run.Out));
    }

    [Fact]
    public async Task DuplicateAddress_IsRejected_AndSameNumberIsAskedAgain()
    {
        AppRun run = await app.RunAsync([], Lines([Urls[0], Urls[1], Urls[0], .. Urls[2..]]), Ct);

        Assert.Equal(0, run.Code);
        Assert.Contains("powtórzony adres", run.Out, StringComparison.Ordinal);
        Assert.Equal(["1", "2", "3", "3", "4", "5"], Prompts(run.Out));
    }

    [Fact]
    public async Task SurroundingSpaces_AreIgnored()
    {
        AppRun run = await app.RunAsync([], Lines([.. Urls.Select(u => "  " + u + "\t")]), Ct);

        Assert.Equal(0, run.Code);
        Assert.Equal(5, Prompts(run.Out).Count);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string Lines(IEnumerable<string> lines) => string.Join("\n", lines) + "\n";

    private static List<string> Prompts(string stdout) =>
        [.. PromptPattern().Matches(stdout).Select(m => m.Groups[1].Value)];

    [GeneratedRegex(@"Podaj adres regulaminu (\d) z 5: ")]
    private static partial Regex PromptPattern();
}
