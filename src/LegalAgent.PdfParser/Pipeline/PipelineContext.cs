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

    /// <summary>Report builder for warnings and statistics.</summary>
    public ReportBuilder Report { get; }

    /// <summary>Cancellation token of the conversion.</summary>
    public CancellationToken CancellationToken { get; }

    /// <summary>Body text metrics; set by the line assembly stage.</summary>
    public BodyStyle? BodyStyle { get; set; }

    /// <summary>The resulting document; set by the document build stage.</summary>
    public LegalDocument? Document { get; set; }
}
