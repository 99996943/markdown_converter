using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using LegalAgent.PdfParser.Layout;
using LegalAgent.PdfParser.Pipeline;
using LegalAgent.PdfParser.Text;

namespace LegalAgent.PdfParser.Stages;

/// <summary>
/// Unicode normalisation of glyph text (FR-010, research R7): ligature expansion, special spaces to a plain
/// space, combining marks merged into their base glyph, NFC (never NFKC, which would destroy superscript
/// digits), and conversion of raised small-font digits after <c>Art. N</c> / <c>§ N</c> to superscript characters.
/// </summary>
public sealed partial class TextNormalizationStage : IPipelineStage
{
    private const double MaxSuperscriptSizeRatio = 0.8;
    private const double MinRaiseRatio = 0.15;
    private const int TailGlyphCount = 24;

    private static readonly char[] SuperscriptDigits =
        ['⁰', '¹', '²', '³', '⁴', '⁵', '⁶', '⁷', '⁸', '⁹'];

    /// <inheritdoc />
    public int Order => StageOrder.TextNormalization;

    /// <inheritdoc />
    public void Execute(PipelineContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (LayoutPage page in context.Pages)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            Normalize(page);
            ReportUnmapped(context, page);
        }
    }

    /// <summary>
    /// Glyphs without a Unicode mapping reach the text as U+FFFD or control characters; their number is reported per
    /// page (FR-070, <c>TXT001_UnmappedGlyphs</c>) so that garbled text is never a silent success.
    /// </summary>
    private static void ReportUnmapped(PipelineContext context, LayoutPage page)
    {
        int unmapped = page.Glyphs.Count(g => g.Text.Any(c => c == '�' || (char.IsControl(c) && !char.IsWhiteSpace(c))));
        if (unmapped > 0)
        {
            context.Report.AddWarning(
                "TXT001_UnmappedGlyphs",
                page.Number,
                string.Create(CultureInfo.InvariantCulture, $"Na stronie {page.Number} {unmapped} znaków nie ma odwzorowania Unicode."));
        }
    }

    private static void Normalize(LayoutPage page)
    {
        var result = new List<LayoutGlyph>(page.Glyphs.Count);
        LayoutGlyph? reference = null;

        foreach (LayoutGlyph glyph in page.Glyphs)
        {
            string text = Ligatures.Expand(MapSpaces(glyph.Text));

            if (result.Count > 0 && IsCombiningMarkOnly(text) && result[^1].Text.Trim().Length > 0)
            {
                LayoutGlyph last = result[^1];
                result[^1] = last with { Text = last.Text + text, Box = last.Box.Union(glyph.Box) };
                continue;
            }

            if (reference is not null && IsRaisedSmallDigit(glyph, text, reference) && FollowsLegalNumber(result))
            {
                result.Add(glyph with { Text = SuperscriptDigits[text[0] - '0'].ToString() });
                continue;
            }

            LayoutGlyph normalized = glyph with { Text = text };
            result.Add(normalized);
            reference = normalized;
        }

        for (int i = 0; i < result.Count; i++)
        {
            string composed = result[i].Text.Normalize(NormalizationForm.FormC);
            if (!string.Equals(composed, result[i].Text, StringComparison.Ordinal))
            {
                result[i] = result[i] with { Text = composed };
            }
        }

        page.Glyphs.Clear();
        foreach (LayoutGlyph g in result)
        {
            page.Glyphs.Add(g);
        }
    }

    private static string MapSpaces(string text)
    {
        bool needsMapping = false;
        foreach (char c in text)
        {
            if (IsSpecialSpace(c))
            {
                needsMapping = true;
                break;
            }
        }

        if (!needsMapping)
        {
            return text;
        }

        var sb = new StringBuilder(text.Length);
        foreach (char c in text)
        {
            sb.Append(IsSpecialSpace(c) ? ' ' : c);
        }

        return sb.ToString();
    }

    private static bool IsSpecialSpace(char c) =>
        c is ' ' or ' ' or '　' or ' ' || c is >= ' ' and <= ' ';

    private static bool IsCombiningMarkOnly(string text)
    {
        if (text.Length == 0)
        {
            return false;
        }

        foreach (char c in text)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsRaisedSmallDigit(LayoutGlyph glyph, string text, LayoutGlyph reference) =>
        text.Length == 1
        && text[0] is >= '0' and <= '9'
        && glyph.PointSize <= reference.PointSize * MaxSuperscriptSizeRatio
        && glyph.Baseline <= reference.Baseline - (reference.PointSize * MinRaiseRatio);

    private static bool FollowsLegalNumber(List<LayoutGlyph> result)
    {
        int start = Math.Max(0, result.Count - TailGlyphCount);
        var tail = new StringBuilder();
        for (int i = start; i < result.Count; i++)
        {
            tail.Append(result[i].Text);
        }

        return LegalNumberTail().IsMatch(tail.ToString());
    }

    [GeneratedRegex("(?:Art\\.|§)\\s*\\d+[a-z]*[⁰¹²³⁴-⁹]*$", RegexOptions.CultureInvariant)]
    private static partial Regex LegalNumberTail();
}
