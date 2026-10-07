using System.Diagnostics;
using System.Globalization;
using LegalAgent.PdfParser.Model;
using LegalAgent.PdfParser.Tests.Fixtures;

namespace LegalAgent.PdfParser.Tests;

/// <summary>
/// Performance and memory-scaling checks (SC-007). Run separately in CI via the Performance category.
/// </summary>
[Trait("Category", "Performance")]
public sealed class PerformanceTests
{
    private static async Task<PdfConversionResult> ConvertAsync(byte[] pdf)
    {
        using var stream = new MemoryStream(pdf);
        return await PdfMarkdownConverter.CreateDefault()
            .ConvertAsync(stream, cancellationToken: TestContext.Current.CancellationToken);
    }

    private static byte[] SyntheticDocument(int pages)
    {
        var b = new SyntheticPdfBuilder()
            .Title("Dokument wydajnościowy")
            .RunningHeader("Regulamin testowy – Bank Przykładowy S.A.")
            .PageNumberFooter("Strona {n} z " + pages.ToString(CultureInfo.InvariantCulture));

        for (int p = 0; p < pages; p++)
        {
            b.Page();
            for (int line = 0; line < 40; line++)
            {
                b.Text(72, 90 + (line * 16), $"Strona {p + 1}, wiersz {line + 1}: Bank prowadzi rachunek zgodnie z przepisami prawa oraz umową.");
            }
        }

        return b.Build();
    }

    /// <summary>
    /// Bytes allocated process-wide during one conversion. Cumulative allocation is used as a robust
    /// proxy for memory demand: peak working set cannot be reset inside a process and is noisy.
    /// </summary>
    private static async Task<long> AllocatedDuringConversionAsync(byte[] pdf)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        long before = GC.GetTotalAllocatedBytes(precise: true);
        PdfConversionResult result = await ConvertAsync(pdf);
        long after = GC.GetTotalAllocatedBytes(precise: true);
        Assert.True(result.IsComplete);
        return after - before;
    }

    [Fact]
    public async Task HundredAndFourteenPageAct_ConvertsInUnderTenSeconds()
    {
        // Warm-up on a small document so JIT is not part of the measurement.
        await ConvertAsync(SyntheticDocument(3));

        byte[] pdf = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Corpus", "acts", "dz-u-2024-30-uslugi-platnicze.pdf"));

        var sw = Stopwatch.StartNew();
        PdfConversionResult result = await ConvertAsync(pdf);
        sw.Stop();

        TestContext.Current.SendDiagnosticMessage($"114-page act converted in {sw.Elapsed.TotalSeconds:F2} s");
        Assert.True(result.IsComplete);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(10), $"Conversion took {sw.Elapsed.TotalSeconds:F2} s");
    }

    [Fact]
    public async Task Memory_GrowsRoughlyLinearlyWithPageCount()
    {
        byte[] small = SyntheticDocument(100);
        byte[] large = SyntheticDocument(200);

        await ConvertAsync(SyntheticDocument(3)); // warm-up

        long allocSmall = await AllocatedDuringConversionAsync(small);
        long allocLarge = await AllocatedDuringConversionAsync(large);

        double ratio = (double)allocLarge / allocSmall;
        TestContext.Current.SendDiagnosticMessage($"Allocated 100 pages: {allocSmall:N0} B, 200 pages: {allocLarge:N0} B, ratio {ratio:F2}");
        Assert.True(ratio < 2.5, $"Allocation ratio 200/100 pages = {ratio:F2} (100: {allocSmall:N0} B, 200: {allocLarge:N0} B)");
    }
}
