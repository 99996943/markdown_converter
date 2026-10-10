using MBank.FaqGenerator.Tests.Fakes;

namespace MBank.FaqGenerator.Tests;

public sealed class HelpTests : IDisposable
{
    private readonly AppHarness app = new();

    public void Dispose() => app.Dispose();

    [Theory]
    [InlineData("--faq-output <katalog>")]
    [InlineData("FAQ_mBank.md")]
    [InlineData("AzureOpenAI:Endpoint")]
    [InlineData("AzureOpenAI:Deployment")]
    [InlineData("Faq:OutputDirectory")]
    [InlineData("FAQGEN__AzureOpenAI__Endpoint")]
    [InlineData("Klucz API")]
    [InlineData("gwiazdk")]
    [InlineData("potokiem")]
    [InlineData("nigdy opcją ani zmienną")]
    [InlineData("scripts/azure/create-openai.sh")]
    public async Task Help_DescribesFaqStage(string fragment)
    {
        AppRun run = await app.RunAsync(["--help"], "", TestContext.Current.CancellationToken);

        Assert.Equal(0, run.Code);
        Assert.Contains(fragment, run.Out, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("0 ")]
    [InlineData("1 ")]
    [InlineData("2 ")]
    [InlineData("3 ")]
    [InlineData("4 ")]
    [InlineData("5 ")]
    [InlineData("6 ")]
    [InlineData("7 ")]
    [InlineData("130 ")]
    public async Task Help_ListsEveryExitCode(string code)
    {
        AppRun run = await app.RunAsync(["--help"], "", TestContext.Current.CancellationToken);

        string codes = run.Out[run.Out.IndexOf("Kody wyjścia:", StringComparison.Ordinal)..];
        Assert.Contains("\n  " + code, codes.ReplaceLineEndings("\n"), StringComparison.Ordinal);
    }
}
