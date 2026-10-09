using System.Collections.Concurrent;
using LegalAgent.Corpus.Composition;
using LegalAgent.Corpus.Pdf;

namespace LegalAgent.Corpus.Typesetting;

/// <summary>
/// A word fragment of one style; <see cref="Glued"/> = no space before it (e.g. a footnote marker);
/// <see cref="Footnote"/> = the footnote number when the token is a footnote marker.
/// </summary>
internal sealed record Token(string Text, InlineStyle Style, bool Glued, int Footnote = 0);

/// <summary>A set line.</summary>
internal sealed record SetLine(IReadOnlyList<Token> Tokens)
{
    public string Text => string.Concat(Tokens.Select((t, i) => (i > 0 && !t.Glued ? " " : string.Empty) + t.Text));

    /// <summary>The words of the line without footnote markers (what the reference truth records).</summary>
    public string ContentText => string.Concat(Tokens.Select((t, i) => t.Footnote > 0 ? string.Empty : (i > 0 && !t.Glued ? " " : string.Empty) + t.Text));
}

/// <summary>Measured text widths (cached; the PdfPig measurement is slow and serialised) and line breaking.</summary>
internal static class TextMeasure
{
    private static readonly ConcurrentDictionary<(string Text, double Size, InlineStyle Style, bool Mono), double> Widths = new();

    public static double Width(string text, double size, InlineStyle style = InlineStyle.Regular, bool mono = false) =>
        text.Length == 0
            ? 0
            : Widths.GetOrAdd((text, size, style, mono), k => SyntheticPdfBuilder.TextWidth(k.Text, k.Size, k.Style == InlineStyle.Bold, k.Style == InlineStyle.Italic, k.Mono));

    /// <summary>Size of a footnote reference marker relative to the text.</summary>
    public const double MarkerScale = 0.6;

    /// <summary>How far a footnote marker is raised, relative to the text size.</summary>
    public const double MarkerRaise = 0.38;

    /// <summary>Width of a token; footnote markers are set smaller.</summary>
    public static double TokenWidth(Token token, double size) =>
        Width(token.Text, token.Footnote > 0 ? size * MarkerScale : size, token.Style);

    /// <summary>Width of one space in the regular face.</summary>
    public static double Space(double size) => Width("a a", size) - (2 * Width("a", size));

    /// <summary>Splits runs into tokens; footnote references become superscript digits glued to the previous token.</summary>
    public static List<Token> Tokenize(IEnumerable<Inline> runs)
    {
        var tokens = new List<Token>();
        bool spaceBefore = true;
        foreach (Inline run in runs)
        {
            switch (run.Kind)
            {
                case InlineKind.FootnoteRef:
                    tokens.Add(new Token(
                        run.Text,
                        InlineStyle.Regular,
                        Glued: tokens.Count > 0,
                        int.Parse(run.Text, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture)));
                    spaceBefore = false;
                    continue;
                case InlineKind.Reference:
                    throw new InvalidOperationException("Unresolved reference in a composed document: " + run.Text);
            }

            string text = run.Text;
            int i = 0;
            while (i < text.Length)
            {
                if (text[i] == ' ')
                {
                    spaceBefore = true;
                    i++;
                    continue;
                }

                int j = text.IndexOf(' ', i);
                if (j < 0)
                {
                    j = text.Length;
                }

                tokens.Add(new Token(text[i..j], run.Style, Glued: !spaceBefore && tokens.Count > 0));
                spaceBefore = false;
                i = j;
            }
        }

        return tokens;
    }

    /// <summary>
    /// Greedy line breaking on measured widths; glued tokens never start a line and a one-letter word stays with the
    /// following word (Polish typesetting). The first line may have a different width (hanging labels).
    /// </summary>
    public static List<SetLine> Wrap(IReadOnlyList<Token> tokens, double width, double size, double firstWidth = -1)
    {
        // Units = maximal sequences that must not be broken: glued tokens and one-letter words with their successor.
        var units = new List<List<Token>>();
        for (int i = 0; i < tokens.Count; i++)
        {
            if (units.Count > 0 && (tokens[i].Glued || IsSingleLetter(units[^1][^1])))
            {
                units[^1].Add(tokens[i]);
            }
            else
            {
                units.Add([tokens[i]]);
            }
        }

        double space = Space(size);
        var lines = new List<SetLine>();
        var current = new List<Token>();
        double used = 0;
        foreach (List<Token> unit in units)
        {
            double w = UnitWidth(unit, size, space);
            double limit = lines.Count == 0 && firstWidth > 0 ? firstWidth : width;
            if (current.Count > 0 && used + space + w > limit)
            {
                lines.Add(new SetLine(current));
                current = [];
                used = 0;
            }

            used += (current.Count > 0 ? space : 0) + w;
            current.AddRange(unit);
        }

        if (current.Count > 0)
        {
            lines.Add(new SetLine(current));
        }

        return lines;
    }

    /// <summary>Width of a set line.</summary>
    public static double LineWidth(IReadOnlyList<Token> tokens, double size) => UnitWidth(tokens, size, Space(size));

    private static double UnitWidth(IReadOnlyList<Token> unit, double size, double space)
    {
        double w = 0;
        for (int i = 0; i < unit.Count; i++)
        {
            w += (i > 0 && !unit[i].Glued ? space : 0) + TokenWidth(unit[i], size);
        }

        return w;
    }

    private static bool IsSingleLetter(Token t) => t.Text.Length == 1 && char.IsLetter(t.Text[0]);
}
