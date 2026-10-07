using LegalAgent.PdfParser.Pipeline;

namespace LegalAgent.PdfParser.Stages;

/// <summary>Table detection (FR-060 – FR-066).</summary>
public sealed class TableDetectionStage : IPipelineStage
{
    /// <inheritdoc />
    public int Order => StageOrder.TableDetection;

    /// <inheritdoc />
    public void Execute(PipelineContext context) => throw new NotImplementedException();
}
