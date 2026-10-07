using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using LegalAgent.PdfParser.Model;

namespace LegalAgent.PdfParser.Rendering;

/// <summary>
/// Deterministic Markdown renderer (contracts/markdown-output.md). Blocks are separated by exactly one blank
/// line. A page marker <c>&lt;!-- page: N --&gt;</c> is written on its own line directly before the first block
/// of a page and, inside a paragraph or list item, between words where the source switches page. Lists are
/// rendered as tight nested Markdown lists; tables are rendered by the user story that introduces them, and
/// encountering them here raises <see cref="NotSupportedException"/> instead of silently dropping content.
/// </summary>
public sealed partial class MarkdownRenderer : IMarkdownRenderer
{
    private readonly RenderingOptions _defaults;

    /// <summary>Creates a renderer with default options.</summary>
    public MarkdownRenderer()
        : this(new RenderingOptions())
    {
    }

    /// <summary>Creates a renderer with the given default options.</summary>
    /// <param name="defaults">Options used when <see cref="Render"/> is called without options.</param>
    public MarkdownRenderer(RenderingOptions defaults)
    {
        ArgumentNullException.ThrowIfNull(defaults);
        _defaults = defaults.Clone();
    }

    /// <inheritdoc />
    public string Render(LegalDocument document, RenderingOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        var state = new State(options ?? _defaults);

        if (!string.IsNullOrWhiteSpace(document.Title))
        {
            state.Chunks.Add("# " + MarkdownEscaper.EscapeText(document.Title.Trim()));
        }

        RenderBlocks(document.Preamble, state);
        RenderFootnotes(document.PreambleFootnotes, state);
        foreach (Section section in document.Sections)
        {
            RenderSection(section, state);
        }

        return state.Chunks.Count == 0 ? string.Empty : string.Join("\n\n", state.Chunks) + "\n";
    }

    private static void RenderSection(Section section, State state)
    {
        string heading = new string('#', Math.Clamp(section.Level, 1, 6)) + " " + MarkdownEscaper.EscapeText(section.HeadingText.Trim());
        state.Chunks.Add(MarkerLine(section.Pages.First, state) + heading);
        state.CurrentPage = section.Pages.First;

        RenderBlocks(section.Blocks, state);
        foreach (Section child in section.Children)
        {
            RenderSection(child, state);
        }

        RenderFootnotes(section.Footnotes, state);
    }

    private static void RenderFootnotes(IReadOnlyList<Footnote> footnotes, State state)
    {
        foreach (Footnote footnote in footnotes)
        {
            string number = footnote.Number.ToString(CultureInfo.InvariantCulture);
            state.Chunks.Add($"[^{number}]: " + RenderInlines(footnote.Inlines, state, trackPages: false));
        }
    }

    private static void RenderBlocks(IReadOnlyList<ContentBlock> blocks, State state)
    {
        foreach (ContentBlock block in blocks)
        {
            switch (block)
            {
                case ParagraphBlock paragraph:
                    string marker = MarkerLine(paragraph.Pages.First, state);
                    state.CurrentPage = paragraph.Pages.First;
                    string text = RenderInlines(paragraph.Inlines, state, trackPages: true);
                    state.CurrentPage = Math.Max(state.CurrentPage, paragraph.Pages.Last);
                    state.Chunks.Add(marker + text);
                    break;

                case ListBlock list:
                    string listMarker = MarkerLine(list.Pages.First, state);
                    state.CurrentPage = list.Pages.First;
                    var lines = new List<string>();
                    RenderListItems(list.Items, 0, lines, state);
                    state.CurrentPage = Math.Max(state.CurrentPage, list.Pages.Last);
                    state.Chunks.Add(listMarker + string.Join("\n", lines));
                    break;

                case SkippedPageBlock skipped:
                    string reason = skipped.Reason == SkipReason.NoTextLayer ? "no-text-layer" : "read-error";
                    state.Chunks.Add(string.Create(
                        CultureInfo.InvariantCulture,
                        $"<!-- page {skipped.PageNumber} skipped: {reason} -->"));
                    state.CurrentPage = skipped.PageNumber;
                    break;

                default:
                    throw new NotSupportedException(
                        $"Rendering of {block.GetType().Name} is not implemented in this stage of the library.");
            }
        }
    }

    private static void RenderListItems(IReadOnlyList<ListItem> items, int depth, List<string> lines, State state)
    {
        string indent = new(' ', 2 * depth);
        bool needBlank = false;
        foreach (ListItem item in items)
        {
            if (needBlank)
            {
                lines.Add(string.Empty);
                needBlank = false;
            }

            string text = RenderLeadingPage(item.Inlines, indent, lines, state);
            string label = item.LabelKind switch
            {
                ListLabelKind.Bullet => string.Empty,
                ListLabelKind.Dash => item.Label == "-" ? "\\-" : item.Label,
                _ => MarkdownEscaper.EscapeListLabel(item.Label),
            };
            lines.Add((indent + "- " + (label.Length == 0 ? string.Empty : label + " ") + text).TrimEnd());

            foreach (ContentBlock child in item.Children)
            {
                switch (child)
                {
                    case ListBlock nested:
                        if (needBlank)
                        {
                            lines.Add(string.Empty);
                            needBlank = false;
                        }

                        RenderListItems(nested.Items, depth + 1, lines, state);
                        state.CurrentPage = Math.Max(state.CurrentPage, nested.Pages.Last);
                        break;

                    case ParagraphBlock paragraph:
                        string childIndent = new(' ', 2 * (depth + 1));
                        lines.Add(string.Empty);
                        string body = RenderLeadingPage(paragraph.Inlines, childIndent, lines, state);
                        foreach (string line in body.Split('\n'))
                        {
                            lines.Add(line.Length == 0 ? line : childIndent + line);
                        }

                        state.CurrentPage = Math.Max(state.CurrentPage, paragraph.Pages.Last);
                        needBlank = true;
                        break;

                    default:
                        throw new NotSupportedException(
                            $"Rendering of {child.GetType().Name} inside a list item is not implemented.");
                }
            }
        }
    }

    /// <summary>
    /// Strips leading page breaks from <paramref name="inlines"/>, writes a marker line when the page changes,
    /// and renders the rest.
    /// </summary>
    private static string RenderLeadingPage(IReadOnlyList<Inline> inlines, string indent, List<string> lines, State state)
    {
        int skip = 0;
        while (skip < inlines.Count && inlines[skip] is PageBreak leading)
        {
            if (state.Options.PageMarkers && leading.PageNumber != state.CurrentPage)
            {
                lines.Add(indent + string.Create(CultureInfo.InvariantCulture, $"<!-- page: {leading.PageNumber} -->"));
            }

            state.CurrentPage = leading.PageNumber;
            skip++;
        }

        return RenderInlines(skip == 0 ? inlines : inlines.Skip(skip).ToList(), state, trackPages: true);
    }

    private static string MarkerLine(int page, State state) =>
        state.Options.PageMarkers && page != state.CurrentPage
            ? string.Create(CultureInfo.InvariantCulture, $"<!-- page: {page} -->\n")
            : string.Empty;

    private static string RenderInlines(IReadOnlyList<Inline> inlines, State state, bool trackPages)
    {
        var sb = new StringBuilder();
        foreach (Inline inline in MergeRuns(inlines, state.Options.EmphasisInline))
        {
            switch (inline)
            {
                case TextRun run:
                    AppendRun(sb, run, state.Options.EmphasisInline);
                    break;

                case FootnoteRef reference:
                    sb.Append(string.Create(CultureInfo.InvariantCulture, $"[^{reference.FootnoteNumber}]"));
                    break;

                case PageBreak pageBreak:
                    if (trackPages)
                    {
                        state.CurrentPage = pageBreak.PageNumber;
                    }

                    AppendPageBreak(sb, pageBreak.PageNumber, state.Options.PageMarkers);
                    break;
            }
        }

        string collapsed = MultipleSpaces().Replace(sb.ToString(), " ");
        return string.Join("\n", collapsed.Split('\n').Select(l => l.TrimEnd())).Trim();
    }

    private static List<Inline> MergeRuns(IReadOnlyList<Inline> inlines, bool emphasis)
    {
        var merged = new List<Inline>(inlines.Count);
        foreach (Inline inline in inlines)
        {
            if (inline is TextRun run)
            {
                TextStyle style = emphasis ? run.Style : TextStyle.None;
                if (merged.Count > 0 && merged[^1] is TextRun last && last.Style == style)
                {
                    merged[^1] = new TextRun(last.Text + run.Text, style);
                }
                else
                {
                    merged.Add(new TextRun(run.Text, style));
                }
            }
            else
            {
                merged.Add(inline);
            }
        }

        return merged;
    }

    private static void AppendRun(StringBuilder sb, TextRun run, bool emphasis)
    {
        bool atLineStart = IsBlank(sb) || sb[^1] == '\n';
        string text = run.Text;

        if (!emphasis || run.Style == TextStyle.None)
        {
            if (atLineStart)
            {
                text = text.TrimStart();
            }

            sb.Append(MarkdownEscaper.EscapeText(text, atLineStart));
            return;
        }

        string core = text.Trim();
        if (core.Length == 0)
        {
            sb.Append(' ');
            return;
        }

        string lead = text[..(text.Length - text.TrimStart().Length)];
        string trail = text[(text.TrimEnd().Length)..];
        string marker = run.Style switch
        {
            TextStyle.Bold => "**",
            TextStyle.Italic => "*",
            _ => "***",
        };

        sb.Append(lead).Append(marker).Append(MarkdownEscaper.EscapeText(core)).Append(marker).Append(trail);
    }

    private static void AppendPageBreak(StringBuilder sb, int page, bool markers)
    {
        if (IsBlank(sb))
        {
            if (markers)
            {
                sb.Clear().Append(string.Create(CultureInfo.InvariantCulture, $"<!-- page: {page} -->\n"));
            }

            return;
        }

        while (sb.Length > 0 && sb[^1] == ' ')
        {
            sb.Length--;
        }

        sb.Append(' ');
        if (markers)
        {
            sb.Append(string.Create(CultureInfo.InvariantCulture, $"<!-- page: {page} --> "));
        }
    }

    private static bool IsBlank(StringBuilder sb)
    {
        for (int i = 0; i < sb.Length; i++)
        {
            if (!char.IsWhiteSpace(sb[i]))
            {
                return false;
            }
        }

        return true;
    }

    [GeneratedRegex(" {2,}", RegexOptions.CultureInvariant)]
    private static partial Regex MultipleSpaces();

    private sealed class State(RenderingOptions options)
    {
        public RenderingOptions Options { get; } = options;

        public List<string> Chunks { get; } = [];

        public int CurrentPage { get; set; }
    }
}
