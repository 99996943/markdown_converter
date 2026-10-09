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
    private readonly IMarkdownRenderer _renderer;
    private readonly RenderingOptions _options;

    /// <summary>Creates a renderer; page markers are switched off whatever <paramref name="options"/> says.</summary>
    public FragmentRenderer(IMarkdownRenderer renderer, RenderingOptions options)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(options);
        _renderer = renderer;
        _options = ChunkingOptions.CopyRendering(options);
    }

    /// <summary>Renders a part of <paramref name="unit"/> (null for the preamble), without a trailing line feed.</summary>
    public string Render(Section? unit, IReadOnlyList<ContentBlock> blocks, IReadOnlyList<Footnote> footnotes) =>
        _renderer is null || _options is null ? string.Empty : throw new NotImplementedException();
}
