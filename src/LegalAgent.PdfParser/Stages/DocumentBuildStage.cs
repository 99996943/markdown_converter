using LegalAgent.PdfParser.Layout;
using LegalAgent.PdfParser.Model;
using LegalAgent.PdfParser.Pipeline;

namespace LegalAgent.PdfParser.Stages;

/// <summary>
/// Builds the <see cref="LegalDocument"/> from the assembled blocks (FR-002, FR-026, FR-043): the document title,
/// the preamble, the section tree from heading levels (with <see cref="Section.Path"/> and page ranges), skipped-page
/// markers in page order, and footnotes numbered globally by first reference and attached to the smallest section
/// that references them first.
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

        var root = new SectionBuilder(null, 0);
        string? title = null;
        var open = new Stack<SectionBuilder>();
        SectionBuilder Current() => open.Count > 0 ? open.Peek() : root;

        var skipped = new Queue<(int Page, SkipReason Reason)>(
            context.Pages
                .Where(p => p.Skipped is not null)
                .OrderBy(p => p.Number)
                .Select(p => (p.Number, p.Skipped!.Value)));

        foreach (LayoutBlock block in context.Blocks)
        {
            while (skipped.Count > 0 && skipped.Peek().Page < block.Pages.First)
            {
                Current().Blocks.Add(SkippedBlock(skipped.Dequeue()));
            }

            if (block.Kind != LayoutBlockKind.Heading)
            {
                Current().Blocks.Add(Convert(block, context.Report));
                continue;
            }

            HeadingInfo heading = block.Heading
                ?? throw new InvalidOperationException("Blok nagłówka nie zawiera informacji o nagłówku.");
            if (heading.Kind == SectionKind.DocumentTitle && title is null && root.Children.Count == 0)
            {
                title = heading.Text;
                continue;
            }

            while (open.Count > 0 && open.Peek().Heading!.Level >= heading.Level)
            {
                open.Pop();
            }

            var section = new SectionBuilder(heading, block.Pages.First);
            Current().Children.Add(section);
            open.Push(section);
        }

        while (skipped.Count > 0)
        {
            Current().Blocks.Add(SkippedBlock(skipped.Dequeue()));
        }

        Dictionary<int, int> numbers = NumberFootnotes(context, root);

        var paths = new List<string>();
        context.Document = new LegalDocument(
            context.Source,
            title,
            root.Blocks.Select(b => Renumber(b, numbers)).ToArray(),
            root.Footnotes.OrderBy(f => f.Number).ToArray(),
            root.Children.Select(c => Build(c, paths, numbers, context.Report)).ToArray());
    }

    /// <summary>
    /// Assigns global numbers by the first reference in document order and attaches each definition to the section
    /// holding that reference; unreferenced definitions go to the deepest section open on their page.
    /// </summary>
    private static Dictionary<int, int> NumberFootnotes(PipelineContext context, SectionBuilder root)
    {
        Dictionary<int, FootnoteDraft> drafts = context.Footnotes.ToDictionary(d => d.Id);
        var numbers = new Dictionary<int, int>();
        var inOrder = new List<SectionBuilder>();

        void Visit(SectionBuilder section)
        {
            if (section.Heading is not null)
            {
                inOrder.Add(section);
            }

            foreach (FootnoteRef reference in section.Blocks.SelectMany(InlinesOf).OfType<FootnoteRef>())
            {
                if (drafts.TryGetValue(reference.FootnoteNumber, out FootnoteDraft? draft) && !numbers.ContainsKey(draft.Id))
                {
                    numbers[draft.Id] = numbers.Count + 1;
                    section.Footnotes.Add(ToFootnote(draft, numbers[draft.Id]));
                }
            }

            foreach (SectionBuilder child in section.Children)
            {
                Visit(child);
            }
        }

        Visit(root);

        foreach (FootnoteDraft draft in context.Footnotes.Where(d => !numbers.ContainsKey(d.Id)).OrderBy(d => d.Page).ThenBy(d => d.Id))
        {
            numbers[draft.Id] = numbers.Count + 1;
            SectionBuilder owner = inOrder.LastOrDefault(s => s.FirstPage <= draft.Page) ?? root;
            owner.Footnotes.Add(ToFootnote(draft, numbers[draft.Id]));
        }

        for (int i = 0; i < numbers.Count; i++)
        {
            context.Report.AddFootnote();
        }
        return numbers;
    }

    private static Footnote ToFootnote(FootnoteDraft draft, int number) =>
        new(number, draft.Label, draft.Inlines.ToArray(), draft.Page, draft.IsOrphan);

    private static Section Build(SectionBuilder builder, List<string> parentPath, Dictionary<int, int> numbers, ReportBuilder report)
    {
        HeadingInfo heading = builder.Heading!;
        List<string> path = [.. parentPath, heading.Text];
        report.AddHeading(heading.Level);

        ContentBlock[] blocks = builder.Blocks.Select(b => Renumber(b, numbers)).ToArray();
        Section[] children = builder.Children.Select(c => Build(c, path, numbers, report)).ToArray();

        int last = new[] { builder.FirstPage }
            .Concat(blocks.Select(b => b.Pages.Last))
            .Concat(children.Select(c => c.Pages.Last))
            .Max();

        return new Section(
            heading.Level,
            heading.Kind,
            heading.Designation,
            heading.Number,
            heading.Title,
            heading.Text,
            path,
            new PageRange(builder.FirstPage, last),
            blocks,
            builder.Footnotes.OrderBy(f => f.Number).ToArray(),
            children);
    }

    /// <summary>Replaces draft identifiers in footnote references with global numbers.</summary>
    private static ContentBlock Renumber(ContentBlock block, Dictionary<int, int> numbers) => block switch
    {
        ParagraphBlock paragraph when paragraph.Inlines.Any(i => i is FootnoteRef) => paragraph with
        {
            Inlines = Renumber(paragraph.Inlines, numbers),
        },
        ListBlock list when InlinesOf(list).Any(i => i is FootnoteRef) => list with
        {
            Items = list.Items
                .Select(item => item with
                {
                    Inlines = Renumber(item.Inlines, numbers),
                    Children = item.Children.Select(c => Renumber(c, numbers)).ToArray(),
                })
                .ToArray(),
        },
        _ => block,
    };

    private static Inline[] Renumber(IReadOnlyList<Inline> inlines, Dictionary<int, int> numbers) => inlines
        .Where(i => i is not FootnoteRef r || numbers.ContainsKey(r.FootnoteNumber))
        .Select(i => i is FootnoteRef r ? new FootnoteRef(numbers[r.FootnoteNumber]) : i)
        .ToArray();

    /// <summary>Inline content of a block in reading order, including nested list items and common parts.</summary>
    private static IEnumerable<Inline> InlinesOf(ContentBlock block) => block switch
    {
        ParagraphBlock paragraph => paragraph.Inlines,
        ListBlock list => list.Items.SelectMany(i => i.Inlines.Concat(i.Children.SelectMany(InlinesOf))),
        _ => [],
    };

    private static SkippedPageBlock SkippedBlock((int Page, SkipReason Reason) skipped) =>
        new(new PageRange(skipped.Page, skipped.Page), skipped.Page, skipped.Reason);

    private static ContentBlock Convert(LayoutBlock block, ReportBuilder report)
    {
        switch (block.Kind)
        {
            case LayoutBlockKind.Paragraph:
                return new ParagraphBlock(block.Pages, block.Inlines.ToArray());

            case LayoutBlockKind.List when block.List is { } list:
                report.AddList();
                return list;

            default:
                throw new InvalidOperationException(
                    $"Blok typu {block.Kind} nie jest jeszcze obsługiwany przez etap budowy dokumentu.");
        }
    }

    private sealed class SectionBuilder(HeadingInfo? heading, int firstPage)
    {
        public HeadingInfo? Heading { get; } = heading;

        public int FirstPage { get; } = firstPage;

        public List<ContentBlock> Blocks { get; } = [];

        public List<SectionBuilder> Children { get; } = [];

        public List<Footnote> Footnotes { get; } = [];
    }
}
