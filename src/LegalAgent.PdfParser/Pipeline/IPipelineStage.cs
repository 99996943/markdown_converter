namespace LegalAgent.PdfParser.Pipeline;

/// <summary>A stateless processing step of the conversion pipeline (advanced extension API).</summary>
public interface IPipelineStage
{
    /// <summary>Execution order; built-in stages use 100, 200, ... 1100 (see <see cref="StageOrder"/>).</summary>
    int Order { get; }

    /// <summary>
    /// Runs the stage. Performs no I/O; all state lives in <paramref name="context"/>.
    /// A thrown exception aborts the conversion.
    /// </summary>
    /// <param name="context">Shared pipeline state.</param>
    void Execute(PipelineContext context);
}
