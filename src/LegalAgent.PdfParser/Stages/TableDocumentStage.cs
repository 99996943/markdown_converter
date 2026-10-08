using LegalAgent.PdfParser.Pipeline;

namespace LegalAgent.PdfParser.Stages;

/// <summary>
/// Detects table-documents (spec 002, FR-080 – FR-084): a document made of one multi-page table with a full grid of
/// rulings and two columns — a narrow left one with section names and a wide right one with their content.
/// </summary>
public sealed class TableDocumentStage : IPipelineStage
{
    /// <inheritdoc />
    public int Order => StageOrder.TableDocument;

    /// <inheritdoc />
    public void Execute(PipelineContext context) => throw new NotImplementedException();
}
