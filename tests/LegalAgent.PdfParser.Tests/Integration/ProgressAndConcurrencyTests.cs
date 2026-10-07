using LegalAgent.PdfParser.Model;
namespace LegalAgent.PdfParser.Tests.Integration;

/// <summary>US5: progress reporting (FR-072) and thread-safety of one converter instance.</summary>
public sealed class ProgressAndConcurrencyTests
{
    [Fact]
    public async Task Progress_ReportsEveryPageInNonDecreasingOrderPerStage()
    {
        var progress = new SyncProgress();
        using var stream = new MemoryStream(ArtifactCleanupIntegrationTests.BuildUs1Pdf());

        await PdfMarkdownConverter.CreateDefault().ConvertAsync(
            stream,
            new PdfConversionRequest { Progress = progress },
            TestContext.Current.CancellationToken);

        Assert.NotEmpty(progress.Reports);
        Assert.All(progress.Reports, r => Assert.Equal(6, r.PageCount));

        foreach (IGrouping<string, ConversionProgress> stage in progress.Reports.GroupBy(r => r.Stage))
        {
            int[] pages = stage.Select(r => r.PageNumber).ToArray();
            Assert.Equal(pages.OrderBy(p => p), pages);
        }

        Assert.Equal(
            [1, 2, 3, 4, 5, 6],
            progress.Reports.Where(r => r.Stage == "PageExtractionStage").Select(r => r.PageNumber));
    }

    [Fact]
    public async Task ParallelConversions_EqualSequentialResults()
    {
        PdfMarkdownConverter converter = PdfMarkdownConverter.CreateDefault();
        byte[] pdf = ArtifactCleanupIntegrationTests.BuildUs1Pdf();

        async Task<PdfConversionResult> One()
        {
            using var stream = new MemoryStream(pdf);
            return await converter.ConvertAsync(stream, new PdfConversionRequest { SourceId = "x.pdf" }, CancellationToken.None);
        }

        PdfConversionResult sequential = await One();
        PdfConversionResult[] parallel = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(One)));

        Assert.All(parallel, r =>
        {
            Assert.Equal(sequential.Markdown, r.Markdown);
            Assert.Equal(sequential.IsComplete, r.IsComplete);
            Assert.Equal(sequential.Report.Warnings.Count, r.Report.Warnings.Count);
            Assert.Equal(sequential.Report.RemovedArtifacts.Count, r.Report.RemovedArtifacts.Count);
        });
    }

    private sealed class SyncProgress : IProgress<ConversionProgress>
    {
        public List<ConversionProgress> Reports { get; } = [];

        public void Report(ConversionProgress value) => Reports.Add(value);
    }
}
