using LegalAgent.PdfParser.Model;
using LegalAgent.PdfParser.Pipeline;

namespace LegalAgent.PdfParser.Tests.Unit.Pipeline;

public sealed class PipelineRunnerTests
{
    private static PipelineContext NewContext(CancellationToken token) =>
        new(new PdfParserOptions(), new SourceInfo("test", 1, null, 0, "00"), new ReportBuilder(), token);

    [Fact]
    public void Run_ExecutesStagesInAscendingOrder()
    {
        var log = new List<string>();
        var runner = new PipelineRunner(
        [
            new RecordingStage(300, "c", log),
            new RecordingStage(100, "a", log),
            new RecordingStage(200, "b", log),
        ]);

        runner.Run(NewContext(TestContext.Current.CancellationToken));

        Assert.Equal(["a", "b", "c"], log);
    }

    [Fact]
    public void Run_BreaksTiesByFullTypeNameOrdinal()
    {
        var log = new List<string>();
        var runner = new PipelineRunner(
        [
            new TieStageB(log),
            new TieStageA(log),
        ]);

        runner.Run(NewContext(TestContext.Current.CancellationToken));

        Assert.Equal(["TieStageA", "TieStageB"], log);
    }

    [Fact]
    public void Run_ChecksCancellationBeforeEachStage()
    {
        using var cts = new CancellationTokenSource();
        var log = new List<string>();
        var runner = new PipelineRunner(
        [
            new CancellingStage(100, cts, log),
            new RecordingStage(200, "second", log),
        ]);

        Assert.Throws<OperationCanceledException>(() => runner.Run(NewContext(cts.Token)));
        Assert.Equal(["cancelling"], log);
    }

    [Fact]
    public void Run_AlreadyCancelledToken_RunsNoStage()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var log = new List<string>();
        var runner = new PipelineRunner([new RecordingStage(100, "a", log)]);

        Assert.Throws<OperationCanceledException>(() => runner.Run(NewContext(cts.Token)));
        Assert.Empty(log);
    }

    [Fact]
    public void Run_WrapsForeignExceptionPreservingInner()
    {
        var inner = new InvalidOperationException("boom");
        var runner = new PipelineRunner([new ThrowingStage(inner)]);

        PdfParserException ex = Assert.Throws<PdfParserException>(() => runner.Run(NewContext(TestContext.Current.CancellationToken)));

        Assert.Same(inner, ex.InnerException);
        Assert.Contains(nameof(ThrowingStage), ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_PdfParserExceptionPropagatesUnchanged()
    {
        var original = new PdfNoTextException();
        var runner = new PipelineRunner([new ThrowingStage(original)]);

        PdfParserException ex = Assert.Throws<PdfNoTextException>(() => runner.Run(NewContext(TestContext.Current.CancellationToken)));

        Assert.Same(original, ex);
    }

    [Fact]
    public void Run_OperationCanceledFromStagePropagatesUnchanged()
    {
        var original = new OperationCanceledException("stage cancelled");
        var runner = new PipelineRunner([new ThrowingStage(original)]);

        OperationCanceledException ex = Assert.Throws<OperationCanceledException>(() => runner.Run(NewContext(TestContext.Current.CancellationToken)));

        Assert.Same(original, ex);
    }

    [Fact]
    public void Constructor_NullStages_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new PipelineRunner(null!));
    }

    private sealed class RecordingStage(int order, string name, List<string> log) : IPipelineStage
    {
        public int Order { get; } = order;

        public void Execute(PipelineContext context) => log.Add(name);
    }

    private sealed class CancellingStage(int order, CancellationTokenSource cts, List<string> log) : IPipelineStage
    {
        public int Order { get; } = order;

        public void Execute(PipelineContext context)
        {
            log.Add("cancelling");
            cts.Cancel();
        }
    }

    private sealed class ThrowingStage(Exception exception) : IPipelineStage
    {
        public int Order => 100;

        public void Execute(PipelineContext context) => throw exception;
    }

    private sealed class TieStageA(List<string> log) : IPipelineStage
    {
        public int Order => 500;

        public void Execute(PipelineContext context) => log.Add(nameof(TieStageA));
    }

    private sealed class TieStageB(List<string> log) : IPipelineStage
    {
        public int Order => 500;

        public void Execute(PipelineContext context) => log.Add(nameof(TieStageB));
    }
}
