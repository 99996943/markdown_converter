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
public sealed record LayoutMeasures(int Tbl001, int PipeRows, int LooseLabelRows, int ParagraphText, int TocHeadings, int PageFooters);

/// <summary>Difference of two collections: entries only in the first (<paramref name="Lost"/>) and only in the second (<paramref name="New"/>).</summary>
public sealed record SetDiff(IReadOnlyList<string> Lost, IReadOnlyList<string> New);

/// <summary>Computes <see cref="LayoutMeasures"/> from Markdown and the conversion warnings.</summary>
public static partial class LayoutMetrics
{
    private const string FallbackSeparator = " \\| ";

    /// <summary>Measures <paramref name="markdown"/> with the warnings of <paramref name="report"/>.</summary>
    public static LayoutMeasures Measure(string markdown, ConversionReport report) => Measure(markdown, report.Warnings);

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
    public static int PageNumberParagraphs(string markdown) => throw new NotImplementedException();

    /// <summary>Tokens „a.”, „b.” … / „i.”, „ii.” … of a consecutive run (at least two) that do not start a list item (spec 008).</summary>
    public static int DotEnumLabelsOutsideList(string markdown) => throw new NotImplementedException();

    /// <summary>Unit headings (Dział, Rozdział, Art., §) present only before / only after, level ignored (spec 008).</summary>
    public static SetDiff UnitHeadingDiff(string before, string after) => throw new NotImplementedException();

    /// <summary>Frame phrases found inside a table row (GFM or fallback) (spec 008).</summary>
    public static int FramesAsTable(string markdown, IReadOnlyCollection<string> phrases) => throw new NotImplementedException();

    /// <summary>Frame phrases found inside a list item instead of in a paragraph of their own (spec 008).</summary>
    public static int FramesWoven(string markdown, IReadOnlyCollection<string> phrases) => throw new NotImplementedException();

    /// <summary>Words (no punctuation or Markdown marks) present only before / only after (spec 008).</summary>
    public static SetDiff WordSetDiff(string before, string after) => throw new NotImplementedException();

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
