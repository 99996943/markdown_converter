using LegalAgent.PdfParser.Model;
using LegalAgent.PdfParser.Pipeline;
using LegalAgent.PdfParser.Rendering;

namespace LegalAgent.PdfParser.Tests.Integration;

/// <summary>US5: caller cancellation versus the <c>MaxDuration</c> limit (FR-003, FR-009b, SC-009).</summary>
public sealed class CancellationAndLimitsTests
{
    private static PdfMarkdownConverter Create(IPipelineStage extra, Action<PdfParserOptions>? configure = null)
    {
        var options = new PdfParserOptions();
        configure?.Invoke(options);
        return new PdfMarkdownConverter([.. BuiltInStages.Create(), extra], options, new MarkdownRenderer());
    }

    private static Task<PdfConversionResult> Run(PdfMarkdownConverter converter, CancellationToken token) =>
        Task.Run(async () =>
        {
            using var stream = new MemoryStream(ArtifactCleanupIntegrationTests.BuildUs1Pdf());
            return await converter.ConvertAsync(stream, null, token);
        });

    [Fact]
    public async Task CallerCancelsMidDocument_ThrowsOperationCanceledException()
    {
        using var blocking = new BlockingStage();
        PdfMarkdownConverter converter = Create(blocking);
        using var cts = new CancellationTokenSource();

        Task<PdfConversionResult> task = Run(converter, cts.Token);
        Assert.True(blocking.Entered.Wait(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken));
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
    }

    [Fact]
    public async Task CallerTokenAlreadyCancelled_ThrowsOperationCanceledException()
    {
        PdfMarkdownConverter converter = PdfMarkdownConverter.CreateDefault();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Run(converter, cts.Token));
    }

    [Fact]
    public async Task MaxDurationElapsed_ThrowsPdfLimitExceededExceptionForDuration()
    {
        using var blocking = new BlockingStage();
        PdfMarkdownConverter converter = Create(blocking, o => o.Limits.MaxDuration = TimeSpan.FromMilliseconds(300));

        PdfLimitExceededException ex = await Assert.ThrowsAsync<PdfLimitExceededException>(
            () => Run(converter, CancellationToken.None));

        Assert.Equal(PdfLimit.Duration, ex.Limit);
        Assert.Equal(300, ex.Configured);
        Assert.True(ex.Measured >= 300, $"Measured {ex.Measured}");
    }

    [Fact]
    public async Task MaxDurationNull_DisablesTheLimit()
    {
        PdfMarkdownConverter converter = Create(new SleepStage(TimeSpan.FromMilliseconds(400)), o => o.Limits.MaxDuration = null);

        PdfConversionResult result = await Run(converter, CancellationToken.None);

        Assert.NotEmpty(result.Markdown);
    }

    private sealed class BlockingStage : IPipelineStage, IDisposable
    {
        public ManualResetEventSlim Entered { get; } = new(false);

        public int Order => 150;

        public void Execute(PipelineContext context)
        {
            Entered.Set();
            context.CancellationToken.WaitHandle.WaitOne(TimeSpan.FromSeconds(30));
            context.CancellationToken.ThrowIfCancellationRequested();
        }

        public void Dispose() => Entered.Dispose();
    }

    private sealed class SleepStage(TimeSpan delay) : IPipelineStage
    {
        public int Order => 150;

        public void Execute(PipelineContext context) => Thread.Sleep(delay);
    }
}
