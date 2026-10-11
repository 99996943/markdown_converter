using System.Globalization;
using System.Text.RegularExpressions;
using LegalAgent.PdfParser.Model;

namespace LegalAgent.PdfParser.Tests.Fixtures;

/// <summary>Layout measures of one converted document (spec 007, research R4).</summary>
/// <param name="Tbl001">Number of <c>TBL001_AmbiguousGrid</c> warnings.</param>
/// <param name="PipeRows">Markdown lines containing a fallback cell separator „ \| ”.</param>
/// <param name="LooseLabelRows">Lines starting with a label „N.”, „N/”, „x/” outside a list item.</param>
/// <param name="ParagraphText">Paragraph designations „§ N” (optionally bold, with a title) outside headings.</param>
/// <param name="TocHeadings">Headings ending with leader dots and a page number.</param>
/// <param name="PageFooters">Page numbers „N/M” outside tables.</param>
/// <param name="NumbersExcess">Sum of multiplicities of numbers only in the Markdown, against the PDF text layer (spec 009).</param>
/// <param name="NumbersMissing">Sum of multiplicities of numbers only in the PDF text layer (spec 009).</param>
public sealed record LayoutMeasures(
    int Tbl001,
    int PipeRows,
    int LooseLabelRows,
    int ParagraphText,
    int TocHeadings,
    int PageFooters,
    int NumbersExcess = 0,
    int NumbersMissing = 0);

/// <summary>Difference of two collections: entries only in the first (<paramref name="Lost"/>) and only in the second (<paramref name="New"/>).</summary>
public sealed record SetDiff(IReadOnlyList<string> Lost, IReadOnlyList<string> New);

/// <summary>Computes <see cref="LayoutMeasures"/> from Markdown and the conversion warnings.</summary>
public static partial class LayoutMetrics
{
    private const string FallbackSeparator = " \\| ";

    /// <summary>Measures <paramref name="markdown"/> with the warnings of <paramref name="report"/>.</summary>
    public static LayoutMeasures Measure(string markdown, ConversionReport report) => Measure(markdown, report.Warnings);

    /// <summary>Measures <paramref name="markdown"/> and compares its numbers with the PDF at <paramref name="pdfPath"/> (spec 009).</summary>
    public static LayoutMeasures Measure(string markdown, ConversionReport report, string pdfPath) =>
        Measure(markdown, report, NumberCheck.Compare(markdown, pdfPath, report));

    /// <summary>Measures <paramref name="markdown"/> and takes the number measures from an already computed <paramref name="numbers"/>.</summary>
    public static LayoutMeasures Measure(string markdown, ConversionReport report, NumberDiff numbers) =>
        Measure(markdown, report) with
        {
            NumbersExcess = numbers.Skipped is null ? numbers.Excess.Values.Sum() : 0,
            NumbersMissing = numbers.Skipped is null ? numbers.Missing.Values.Sum() : 0,
        };

    /// <summary>Measures <paramref name="markdown"/> with <paramref name="warnings"/>.</summary>
    public static LayoutMeasures Measure(string markdown, IReadOnlyList<ConversionWarning> warnings)
    {
        int pipeRows = 0, looseLabels = 0, paragraphs = 0, tocHeadings = 0, footers = 0;
        foreach (string line in markdown.Split('\n'))
        {
            bool heading = line.StartsWith('#');
            bool table = line.StartsWith('|') || line.Contains(FallbackSeparator, StringComparison.Ordinal);
            if (line.Contains(FallbackSeparator, StringComparison.Ordinal))
            {
                pipeRows++;
            }

            if (LooseLabel().IsMatch(line))
            {
                looseLabels++;
            }

            if (!heading && (BareParagraph().IsMatch(line) || TitledParagraph().IsMatch(line) || BoldParagraph().IsMatch(line)))
            {
                paragraphs++;
            }

            if (heading && TocHeading().IsMatch(line))
            {
                tocHeadings++;
            }

            if (!table)
            {
                footers += PageOfPages().Matches(line).Count(m => int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) <= int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture));
            }
        }

        int tbl001 = warnings.Count(w => w.Code == "TBL001_AmbiguousGrid");
        return new LayoutMeasures(tbl001, pipeRows, looseLabels, paragraphs, tocHeadings, footers);
    }

    /// <summary>Paragraphs whose whole text is a page number „N/M” (spec 008).</summary>
    public static int PageNumberParagraphs(string markdown) =>
        markdown.Split('\n').Count(line => WholePageNumber().IsMatch(line.Trim()));

    /// <summary>
    /// Tokens „a.”, „b.” … / „i.”, „ii.” … that belong to a run of at least two consecutive labels and do not start a
    /// list item (in item text, in a paragraph, in a table cell) (spec 008). The ambiguous „i.”, „v.”, „x.” count
    /// as a letter and as a Roman numeral; a token is in a run when either reading continues it.
    /// </summary>
    public static int DotEnumLabelsOutsideList(string markdown)
    {
        var tokens = new List<(string Text, bool StartsItem)>();
        foreach (string line in markdown.Split('\n'))
        {
            int itemStart = ListPrefix().Match(line) is { Success: true } prefix ? prefix.Length : -1;
            foreach (Match m in DotToken().Matches(line))
            {
                tokens.Add((m.Groups[1].Value, m.Index == itemStart));
            }
        }

        var inRun = new bool[tokens.Count];
        MarkRuns(tokens, inRun, static t => t.Length == 1 ? t[0] - 'a' + 1 : 0);
        MarkRuns(tokens, inRun, RomanValue);
        return tokens.Where((t, i) => inRun[i] && !t.StartsItem).Count();
    }

    /// <summary>Unit headings (Dział, Rozdział, Art., §) present only before / only after, level ignored (spec 008).</summary>
    public static SetDiff UnitHeadingDiff(string before, string after) =>
        Diff(UnitHeadings(before), UnitHeadings(after));

    /// <summary>Frame phrases found inside a table row (GFM or fallback) (spec 008).</summary>
    public static int FramesAsTable(string markdown, IReadOnlyCollection<string> phrases) =>
        CountPhrases(markdown, phrases, static line => IsTableRow(line));

    /// <summary>Frame phrases found inside a list item instead of in a paragraph of their own (spec 008).</summary>
    public static int FramesWoven(string markdown, IReadOnlyCollection<string> phrases) =>
        CountPhrases(markdown, phrases, static line => !IsTableRow(line) && line.TrimStart().StartsWith("- ", StringComparison.Ordinal));

    /// <summary>Words (no punctuation or Markdown marks) present only before / only after (spec 008).</summary>
    public static SetDiff WordSetDiff(string before, string after) => Diff(Words(before), Words(after));

    private static bool IsTableRow(string line) => line.StartsWith('|') || line.Contains(FallbackSeparator, StringComparison.Ordinal);

    private static int CountPhrases(string markdown, IReadOnlyCollection<string> phrases, Func<string, bool> lineFilter)
    {
        string[] wanted = phrases.Select(Normalize).Where(p => p.Length > 0).Distinct(StringComparer.Ordinal).ToArray();
        int count = 0;
        foreach (string line in markdown.Split('\n').Where(lineFilter))
        {
            string text = Normalize(line);
            foreach (string phrase in wanted)
            {
                for (int at = text.IndexOf(phrase, StringComparison.Ordinal); at >= 0; at = text.IndexOf(phrase, at + phrase.Length, StringComparison.Ordinal))
                {
                    count++;
                }
            }
        }

        return count;
    }

    private static string Normalize(string text) =>
        Whitespace().Replace(text.Replace("**", string.Empty, StringComparison.Ordinal).Replace("\\", string.Empty, StringComparison.Ordinal), " ").Trim();

    private static List<string> UnitHeadings(string markdown) =>
        markdown.Split('\n')
            .Select(line => UnitHeading().Match(line))
            .Where(m => m.Success)
            .Select(m => m.Groups[1].Value.Trim())
            .ToList();

    private static HashSet<string> Words(string markdown) =>
        WordToken().Matches(HtmlComment().Replace(markdown, " ")).Select(m => m.Value.ToLowerInvariant()).ToHashSet(StringComparer.Ordinal);

    private static SetDiff Diff(IEnumerable<string> before, IEnumerable<string> after) =>
        new(Subtract(before, after), Subtract(after, before));

    /// <summary>Multiset difference <paramref name="from"/> minus <paramref name="what"/>, in ordinal order.</summary>
    private static List<string> Subtract(IEnumerable<string> from, IEnumerable<string> what)
    {
        Dictionary<string, int> remaining = what.GroupBy(s => s, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        var result = new List<string>();
        foreach (string item in from.Order(StringComparer.Ordinal))
        {
            if (remaining.TryGetValue(item, out int left) && left > 0)
            {
                remaining[item] = left - 1;
            }
            else
            {
                result.Add(item);
            }
        }

        return result;
    }

    /// <summary>Marks tokens that continue (or are continued by) a +1 sequence of the reading given by <paramref name="value"/>.</summary>
    private static void MarkRuns(List<(string Text, bool StartsItem)> tokens, bool[] inRun, Func<string, int> value)
    {
        int previousIndex = -1;
        int previousValue = 0;
        for (int i = 0; i < tokens.Count; i++)
        {
            int v = value(tokens[i].Text);
            if (v == 0)
            {
                continue;
            }

            if (previousIndex >= 0 && v == previousValue + 1)
            {
                inRun[previousIndex] = true;
                inRun[i] = true;
            }

            previousIndex = i;
            previousValue = v;
        }
    }

    /// <summary>Value of a lower-case Roman numeral i to xxxix, 0 when <paramref name="token"/> is not one.</summary>
    private static int RomanValue(string token)
    {
        if (!RomanNumeral().IsMatch(token))
        {
            return 0;
        }

        int total = 0;
        for (int i = 0; i < token.Length; i++)
        {
            int digit = token[i] switch { 'i' => 1, 'v' => 5, _ => 10 };
            int next = i + 1 < token.Length ? token[i + 1] switch { 'i' => 1, 'v' => 5, _ => 10 } : 0;
            total += digit < next ? -digit : digit;
        }

        return total;
    }

    [GeneratedRegex(@"^\d{1,3}/\d{1,3}$")]
    private static partial Regex WholePageNumber();

    /// <summary>A list marker at the start of a line; the label token follows it.</summary>
    [GeneratedRegex(@"^\s*-\s+")]
    private static partial Regex ListPrefix();

    /// <summary>A candidate label „a.”, „ii.” (the dot may be escaped) followed by text.</summary>
    [GeneratedRegex(@"(?<=^|[\s|])([a-z]|[ivx]+)\\?\.(?=\s)")]
    private static partial Regex DotToken();

    [GeneratedRegex(@"^(?:x{0,3})(?:ix|iv|v?i{0,3})$")]
    private static partial Regex RomanNumeral();

    [GeneratedRegex(@"^#{1,6}\s+((?:Art\.|§|Rozdział|Dział|Oddział|Tytuł)(?=\s|$).*)$")]
    private static partial Regex UnitHeading();

    [GeneratedRegex(@"[\p{L}\p{N}]+")]
    private static partial Regex WordToken();

    [GeneratedRegex(@"<!--.*?-->")]
    private static partial Regex HtmlComment();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    /// <summary>A label „N.” (escaped or bold), „N/” or „x/” at the very start of a line, followed by text.</summary>
    [GeneratedRegex(@"^(?:\*\*)?(?:\d{1,3}[a-z]?(?:\\?\.|/)|[a-z]{1,2}/)(?:\*\*)?\s+\S")]
    private static partial Regex LooseLabel();

    [GeneratedRegex(@"^(?:\*\*)?§\s*\d+[a-z]?\.?(?:\*\*)?$")]
    private static partial Regex BareParagraph();

    /// <summary>„§ 3. Porady ogólne” — a short title without a sentence period.</summary>
    [GeneratedRegex(@"^(?:\*\*)?§\s*\d+[a-z]?\.\s+\p{Lu}[^.]{0,100}(?:\*\*)?$")]
    private static partial Regex TitledParagraph();

    /// <summary>A bold designation glued into running text, e.g. after a page marker.</summary>
    [GeneratedRegex(@"(?:^|\s)\*\*§\s*\d+[a-z]?\.?(?:\s+\p{Lu}[^*]*)?\*\*(?:\s|$)")]
    private static partial Regex BoldParagraph();

    [GeneratedRegex(@"(?:(?:\.\s?){3,}|…+)\s*\d+\s*$")]
    private static partial Regex TocHeading();

    [GeneratedRegex(@"(?<![\d/])(\d{1,3})/(\d{1,3})(?![\d/])")]
    private static partial Regex PageOfPages();
}
