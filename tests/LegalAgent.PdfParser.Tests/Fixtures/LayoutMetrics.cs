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

/// <summary>Computes <see cref="LayoutMeasures"/> from Markdown and the conversion warnings.</summary>
public static class LayoutMetrics
{
    /// <summary>Measures <paramref name="markdown"/> with the warnings of <paramref name="report"/>.</summary>
    public static LayoutMeasures Measure(string markdown, ConversionReport report) => Measure(markdown, report.Warnings);

    /// <summary>Measures <paramref name="markdown"/> with <paramref name="warnings"/>.</summary>
    public static LayoutMeasures Measure(string markdown, IReadOnlyList<ConversionWarning> warnings) =>
        throw new NotImplementedException();
}
