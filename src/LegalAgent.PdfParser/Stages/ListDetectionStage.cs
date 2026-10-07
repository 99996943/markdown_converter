using LegalAgent.PdfParser.Pipeline;

namespace LegalAgent.PdfParser.Stages;

/// <summary>List detection (FR-050 – FR-054).</summary>
public sealed class ListDetectionStage : IPipelineStage
{
    /// <inheritdoc />
    public int Order => StageOrder.ListDetection;

    /// <inheritdoc />
    public void Execute(PipelineContext context) => throw new NotImplementedException();
}
