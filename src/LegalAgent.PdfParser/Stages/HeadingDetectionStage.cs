using LegalAgent.PdfParser.Pipeline;

namespace LegalAgent.PdfParser.Stages;

/// <summary>
/// Detects typographic headings and legal units, assigns heading levels and splits „Art. 5. Treść…” lines into a
/// heading and its first paragraph (FR-040 – FR-047, FR-043a). Not yet part of the built-in pipeline.
/// </summary>
public sealed class HeadingDetectionStage : IPipelineStage
{
    /// <inheritdoc />
    public int Order => StageOrder.HeadingDetection;

    /// <inheritdoc />
    public void Execute(PipelineContext context) => throw new NotImplementedException("T062");
}
