using LegalAgent.PdfParser.Layout;
using LegalAgent.PdfParser.Model;
using LegalAgent.PdfParser.Pipeline;

namespace LegalAgent.PdfParser.Stages;

/// <summary>
/// Builds the <see cref="LegalDocument"/>. In this first version every block goes into
/// <see cref="LegalDocument.Preamble"/>; the section tree, footnotes and detected title are added together with
/// heading and footnote detection. Pages that were skipped become <see cref="SkippedPageBlock"/>s placed in page order.
/// </summary>
public sealed class DocumentBuildStage : IPipelineStage
{
    /// <inheritdoc />
    public int Order => StageOrder.DocumentBuild;

    /// <inheritdoc />
    public void Execute(PipelineContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.CancellationToken.ThrowIfCancellationRequested();

        var skipped = new Queue<(int Page, SkipReason Reason)>(
            context.Pages
                .Where(p => p.Skipped is not null)
                .OrderBy(p => p.Number)
                .Select(p => (p.Number, p.Skipped!.Value)));

        var blocks = new List<ContentBlock>();
        foreach (LayoutBlock block in context.Blocks)
        {
            while (skipped.Count > 0 && skipped.Peek().Page < block.Pages.First)
            {
                blocks.Add(SkippedBlock(skipped.Dequeue()));
            }

            blocks.Add(Convert(block));
        }

        while (skipped.Count > 0)
        {
            blocks.Add(SkippedBlock(skipped.Dequeue()));
        }

        context.Document = new LegalDocument(context.Source, null, blocks, [], []);
    }

    private static SkippedPageBlock SkippedBlock((int Page, SkipReason Reason) skipped) =>
        new(new PageRange(skipped.Page, skipped.Page), skipped.Page, skipped.Reason);

    private static ParagraphBlock Convert(LayoutBlock block) => block.Kind switch
    {
        LayoutBlockKind.Paragraph => new ParagraphBlock(block.Pages, block.Inlines.ToArray()),
        _ => throw new InvalidOperationException(
            $"Blok typu {block.Kind} nie jest jeszcze obsługiwany przez etap budowy dokumentu."),
    };
}
