using LegalAgent.Corpus.Planning;
using LegalAgent.Corpus.Tests.Unit.Content;

namespace LegalAgent.Corpus.Tests.Unit;

public sealed class TemplateCheckTests : IDisposable
{
    private readonly string _out = Path.Combine(Path.GetTempPath(), "corpus-check-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_out))
        {
            Directory.Delete(_out, recursive: true);
        }
    }

    private static RunParameters Parameters(int min, int max) => new()
    {
        Pages = new LegalAgent.Corpus.Planning.PageRange(min, max),
        ContentDirectory = MiniContent.Path,
        MaxSharedShare = 100,
    };

    [Fact]
    public async Task Check_ReportsThePageRangeOfEveryLayoutAndWritesSamples()
    {
        TemplateCheckReport report = await CorpusGenerator.CheckTemplateAsync(Parameters(1, 3), "regulamin-karty", _out, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(("regulamin-karty", "regulaminy"), (report.Template, report.Type));
        Assert.Equal(["jedna-kolumna", "dwie-kolumny"], report.Layouts.Select(l => l.Layout));
        Assert.All(report.Layouts, l => Assert.True(l.MinPages <= l.MaxPages));
        Assert.All(report.Layouts, l => Assert.NotNull(l.FittedPages));
        Assert.Empty(report.Violations);
        Assert.True(File.Exists(Path.Combine(_out, "regulamin-karty.jedna-kolumna.pdf")));
        Assert.True(File.Exists(Path.Combine(_out, "regulamin-karty.dwie-kolumny.md")));
        Assert.True(report.RequiredWords > 0);
    }

    [Fact]
    public async Task Check_UnreachableRange_IsReportedNotThrown()
    {
        TemplateCheckReport report = await CorpusGenerator.CheckTemplateAsync(Parameters(40, 50), "regulamin-karty", null, cancellationToken: TestContext.Current.CancellationToken);

        Assert.All(report.Layouts, l => Assert.Null(l.FittedPages));
        Assert.All(report.Layouts, l => Assert.Contains("nieosiągalny zakres stron", l.FitError, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Check_UnknownTemplate_Throws()
    {
        await Assert.ThrowsAsync<CorpusGenerationException>(() =>
            CorpusGenerator.CheckTemplateAsync(Parameters(1, 3), "nie-ma", null, cancellationToken: TestContext.Current.CancellationToken));
    }
}
