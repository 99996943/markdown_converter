using LegalAgent.PdfParser.Pipeline;

namespace LegalAgent.PdfParser.Stages;

/// <summary>
/// Detects footnote definitions at the bottom of pages and footnote reference markers in the text (FR-026).
/// Not yet part of the built-in pipeline.
/// </summary>
public sealed class FootnoteDetectionStage : IPipelineStage
{
    /// <inheritdoc />
    public int Order => StageOrder.FootnoteDetection;

    /// <inheritdoc />
    public void Execute(PipelineContext context) => throw new NotImplementedException("T063");
}
