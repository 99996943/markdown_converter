using LegalAgent.PdfParser.Layout;
using LegalAgent.PdfParser.Model;

namespace LegalAgent.PdfParser.Pipeline;

/// <summary>Typical body text metrics of a document, computed once the lines are assembled.</summary>
/// <param name="FontSize">Most frequent font size in points.</param>
/// <param name="Leading">Typical distance between consecutive baselines in points.</param>
public sealed record BodyStyle(double FontSize, double Leading);

/// <summary>Shared state passed through the pipeline stages.</summary>
public sealed class PipelineContext
{
    /// <summary>Creates a context.</summary>
    /// <param name="options">Effective options (read-only for stages).</param>
    /// <param name="source">Source metadata.</param>
    /// <param name="report">Report builder.</param>
    /// <param name="cancellationToken">Cancellation token of the conversion.</param>
    public PipelineContext(
        PdfParserOptions options,
        SourceInfo source,
        ReportBuilder report,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(report);
        Options = options;
        Source = source;
        Report = report;
        CancellationToken = cancellationToken;
    }

    /// <summary>Effective options; stages must not modify them.</summary>
    public PdfParserOptions Options { get; }

    /// <summary>Source metadata.</summary>
    public SourceInfo Source { get; }

    /// <summary>
    /// The opened PdfPig document the extraction stage reads from. Set by the facade before the pipeline
    /// runs; the facade owns and disposes it.
    /// </summary>
    internal UglyToad.PdfPig.PdfDocument? SourceDocument { get; set; }

    /// <summary>Working pages.</summary>
    public IList<LayoutPage> Pages { get; } = [];

    /// <summary>Logical blocks produced by block-forming stages.</summary>
    public IList<LayoutBlock> Blocks { get; } = [];

    /// <summary>
    /// Footnote definitions found by footnote detection, in detection order. Until document build, a
    /// <see cref="FootnoteRef"/> inline carries <see cref="FootnoteDraft.Id"/> in its number; document build
    /// renumbers references globally by first occurrence (FR-026).
    /// </summary>
    public IList<FootnoteDraft> Footnotes { get; } = [];

    /// <summary>
    /// Tables found by table detection (blocks with <see cref="LayoutBlock.Table"/>); their lines carry
    /// <see cref="LayoutAnnotations.TableIndex"/> with the index into this list and are emitted by block assembly.
    /// </summary>
    public IList<LayoutBlock> Tables { get; } = [];

    /// <summary>Report builder for warnings and statistics.</summary>
    public ReportBuilder Report { get; }

    /// <summary>Cancellation token of the conversion.</summary>
    public CancellationToken CancellationToken { get; }

    /// <summary>Receiver of per-page progress reports; null when the caller did not ask for progress.</summary>
    public IProgress<ConversionProgress>? Progress { get; init; }

    /// <summary>Name of the stage currently executing; maintained by <see cref="PipelineRunner"/>.</summary>
    public string CurrentStage { get; internal set; } = string.Empty;

    /// <summary>
    /// Reports that <paramref name="pageNumber"/> (1-based) has been processed by the current stage (FR-072).
    /// Stages must call it in ascending page order.
    /// </summary>
    /// <param name="pageNumber">The processed page.</param>
    public void ReportProgress(int pageNumber) =>
        Progress?.Report(new ConversionProgress(pageNumber, Source.PageCount, CurrentStage));

    /// <summary>Body text metrics; set by the line assembly stage.</summary>
    public BodyStyle? BodyStyle { get; set; }

    /// <summary>The resulting document; set by the document build stage.</summary>
    public LegalDocument? Document { get; set; }
}
