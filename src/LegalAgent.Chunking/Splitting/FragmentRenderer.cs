using LegalAgent.PdfParser;
using LegalAgent.PdfParser.Model;
using LegalAgent.PdfParser.Rendering;

namespace LegalAgent.Chunking.Splitting;

/// <summary>
/// Renders the content of one chunk (research R1): a small <see cref="LegalDocument"/> without a title, holding the
/// unit heading, the part's blocks and footnotes, rendered by the parser's renderer without page markers.
/// </summary>
internal sealed class FragmentRenderer
{
    private static readonly SourceInfo NoSource = new(null, 1, null, 0, string.Empty);

    private readonly IMarkdownRenderer _renderer;
    private readonly RenderingOptions _options;

    /// <summary>Creates a renderer; page markers are switched off whatever <paramref name="options"/> says.</summary>
    public FragmentRenderer(IMarkdownRenderer renderer, RenderingOptions options)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(options);
        _renderer = renderer;
        _options = ChunkingOptions.CopyRendering(options);
        _options.PageMarkers = false;
    }

    /// <summary>Renders a part of <paramref name="unit"/> (null for the preamble), without a trailing line feed.</summary>
    public string Render(Section? unit, IReadOnlyList<ContentBlock> blocks, IReadOnlyList<Footnote> footnotes)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        ArgumentNullException.ThrowIfNull(footnotes);

        // A skipped page is rendered as a comment, which is not text of the PDF (spec 004, FR-230).
        List<ContentBlock> content = blocks.Where(b => b is not SkippedPageBlock).ToList();
        LegalDocument document = unit is null
            ? new LegalDocument(NoSource, null, content, footnotes, [])
            : new LegalDocument(NoSource, null, [], [], [unit with { Blocks = content, Footnotes = footnotes, Children = [] }]);
        return _renderer.Render(document, _options).TrimEnd('\n');
    }
}
