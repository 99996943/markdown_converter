using LegalAgent.PdfParser.Model;
using LegalAgent.PdfParser.Tests.Fixtures;

namespace LegalAgent.PdfParser.Tests.Corpus;

/// <summary>
/// T094 — optional private corpus (documents that cannot be committed, e.g. real bank regulations): every <c>*.pdf</c>
/// in the directory named by <c>LEGALAGENT_PRIVATE_CORPUS</c> must convert completely, and must match its
/// <c>*.expected.md</c> when one lies next to it. Skipped when the variable is unset.
/// </summary>
public sealed class PrivateCorpusTests
{
    private const string Variable = "LEGALAGENT_PRIVATE_CORPUS";

    [Fact]
    public async Task PrivateDocuments_ConvertAndMatchTheirGoldens()
    {
        string? directory = Environment.GetEnvironmentVariable(Variable);
        Assert.SkipWhen(string.IsNullOrWhiteSpace(directory), $"{Variable} is not set.");
        Assert.True(Directory.Exists(directory), $"{Variable} points to a missing directory: {directory}");

        string[] pdfs = Directory.GetFiles(directory!, "*.pdf").Order(StringComparer.Ordinal).ToArray();
        Assert.NotEmpty(pdfs);
        foreach (string pdf in pdfs)
        {
            await using FileStream stream = File.OpenRead(pdf);
            PdfConversionResult result = await PdfMarkdownConverter.CreateDefault()
                .ConvertAsync(stream, new PdfConversionRequest { SourceId = Path.GetFileName(pdf) }, TestContext.Current.CancellationToken);

            Assert.True(result.IsComplete, $"{Path.GetFileName(pdf)} converted incompletely.");
            string expected = Path.ChangeExtension(pdf, ".expected.md");
            if (File.Exists(expected))
            {
                GoldenFile.AssertMatches(result.Markdown, expected);
            }
        }
    }
}
