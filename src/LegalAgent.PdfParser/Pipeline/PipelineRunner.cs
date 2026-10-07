namespace LegalAgent.PdfParser.Pipeline;

/// <summary>Runs pipeline stages in ascending <see cref="IPipelineStage.Order"/>.</summary>
public sealed class PipelineRunner
{
    private readonly IPipelineStage[] _stages;

    /// <summary>Creates a runner; stages are sorted by order, ties broken by full type name (ordinal).</summary>
    /// <param name="stages">Stages to run.</param>
    public PipelineRunner(IEnumerable<IPipelineStage> stages)
    {
        ArgumentNullException.ThrowIfNull(stages);
        _stages = stages
            .OrderBy(s => s.Order)
            .ThenBy(s => s.GetType().FullName, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>The stages in execution order.</summary>
    public IReadOnlyList<IPipelineStage> Stages => _stages;

    /// <summary>Runs all stages against <paramref name="context"/>.</summary>
    /// <param name="context">Pipeline state.</param>
    /// <exception cref="OperationCanceledException">The cancellation token was signalled (or a stage threw it).</exception>
    /// <exception cref="PdfParserException">A stage failed; foreign exceptions are wrapped with the original as inner exception.</exception>
    public void Run(PipelineContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (IPipelineStage stage in _stages)
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            try
            {
                stage.Execute(context);
            }
            catch (Exception ex) when (ex is not PdfParserException and not OperationCanceledException)
            {
                throw new PdfParserException(
                    $"Etap potoku {stage.GetType().Name} zakończył się błędem: {ex.Message}",
                    ex);
            }
        }
    }
}
