using LegalAgent.PdfParser.Tests.Fixtures;
namespace LegalAgent.PdfParser.Tests;
public sealed class ZzDumpBanking
{
    [Fact]
    public async Task Dump()
    {
        string dir = Environment.GetEnvironmentVariable("BANK_OUT")!;
        Directory.CreateDirectory(dir);
        foreach ((string name, byte[] pdf) in BankingCorpusGenerator.Documents())
        {
            using var s = new MemoryStream(pdf);
            var r = await PdfMarkdownConverter.CreateDefault().ConvertAsync(s, cancellationToken: TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(Path.Combine(dir, name + ".md"), r.Markdown, TestContext.Current.CancellationToken);
            await File.WriteAllBytesAsync(Path.Combine(dir, name + ".pdf"), pdf, TestContext.Current.CancellationToken);
        }
    }
}
