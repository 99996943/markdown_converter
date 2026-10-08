using LegalAgent.PdfParser.Pipeline;

namespace LegalAgent.PdfParser.Stages;

/// <summary>Detects step schemes (FR-067).</summary>
public sealed class StepSequenceStage : IPipelineStage
{
    /// <inheritdoc />
    public int Order => StageOrder.StepSequence;

    /// <inheritdoc />
    public void Execute(PipelineContext context) => throw new NotImplementedException();
}
