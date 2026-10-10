using LegalAgent.Faq.Tests.Fakes;
using MBank.FaqGenerator.Tests.Fakes;

namespace MBank.FaqGenerator.Tests;

public sealed class SummaryTests : IDisposable
{
    private static readonly string[] Urls = [.. Enumerable.Range(1, 5).Select(i => $"https://www.example.test/pdf/reg-{i}.pdf")];
    private static readonly int[] Sizes = [20480, 831898, 1258291, 24576, 1048576];

    private readonly AppHarness app = new();

    public SummaryTests()
    {
        for (int i = 0; i < Urls.Length; i++)
        {
            app.Http.Pdf(Urls[i], TestPdfs.Padded(TestPdfs.Regulation(Urls[i]), Sizes[i]));
        }
    }

    public void Dispose() => app.Dispose();

    [Fact]
    public async Task Run_PrintsProgressForEveryAddress()
    {
        AppRun run = await app.RunAsync([], Input, Ct);

        Assert.Equal(0, run.Code);
        for (int n = 1; n <= 5; n++)
        {
            Assert.Contains($"[{n}/5] pobieranie {Urls[n - 1]}", run.Out, StringComparison.Ordinal);
            Assert.Contains($"[{n}/5] pobrano reg-{n}.pdf (", run.Out, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Run_PrintsSummaryInIndexOrder_WithPolishSizes()
    {
        AppRun run = await app.RunAsync([], Input, Ct);

        string expected = string.Join(
            "\n",
            $"Pobrano 5 z 5 plików do {app.OutputDirectory}:",
            "  1. reg-1.pdf — 20,0 KB",
            "  2. reg-2.pdf — 812,4 KB",
            "  3. reg-3.pdf — 1,2 MB",
            "  4. reg-4.pdf — 24,0 KB",
            "  5. reg-5.pdf — 1,0 MB",
            $"Manifest: {Path.Combine(app.OutputDirectory, "manifest.json")}");
        Assert.Contains(expected, run.Out.ReplaceLineEndings("\n"), StringComparison.Ordinal);
        Assert.True(
            run.Out.IndexOf("Pobrano 5 z 5", StringComparison.Ordinal) > run.Out.LastIndexOf("] pobrano", StringComparison.Ordinal),
            "the summary follows the progress lines");
    }

    [Theory]
    [InlineData(512, "512 B")]
    [InlineData(1024, "1,0 KB")]
    [InlineData(831898, "812,4 KB")]
    [InlineData(1258291, "1,2 MB")]
    public void FormatSize_PolishUnits(long bytes, string expected)
    {
        Assert.Equal(expected, ConsoleReport.FormatSize(bytes));
    }

    private static string Input => string.Join("\n", Urls) + "\n";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
