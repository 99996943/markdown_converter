using LegalAgent.PdfParser.Input;
using LegalAgent.PdfParser.Pipeline;

namespace LegalAgent.PdfParser.Tests.Fixtures;

/// <summary>Opens a synthetic PDF and builds a <see cref="PipelineContext"/> to run individual stages against.</summary>
internal sealed class StageHarness : IDisposable
{
    private readonly OpenedPdf _opened;

    private StageHarness(OpenedPdf opened, PipelineContext context)
    {
        _opened = opened;
        Context = context;
    }

    public PipelineContext Context { get; }

    public static StageHarness Open(byte[] pdf, Action<PdfParserOptions>? configure = null) =>
        OpenWithToken(pdf, CancellationToken.None, configure);

    public static StageHarness OpenWithToken(byte[] pdf, CancellationToken cancellationToken, Action<PdfParserOptions>? configure = null)
    {
        var options = new PdfParserOptions();
        configure?.Invoke(options);
        var input = new PdfInput(pdf, pdf.Length, new string('0', 64));
        OpenedPdf opened = PdfDocumentOpener.Open(input, "test.pdf", options.Limits);
        var context = new PipelineContext(options, opened.Source, new ReportBuilder(), cancellationToken)
        {
            SourceDocument = opened.Document,
        };
        return new StageHarness(opened, context);
    }

    /// <summary>Runs the given stages in the given order against the context.</summary>
    public StageHarness Run(params IPipelineStage[] stages)
    {
        foreach (IPipelineStage stage in stages)
        {
            stage.Execute(Context);
        }

        return this;
    }

    public void Dispose() => _opened.Dispose();
}
