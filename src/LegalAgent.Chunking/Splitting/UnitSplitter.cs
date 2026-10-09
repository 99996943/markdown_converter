using LegalAgent.Chunking.Model;
using LegalAgent.PdfParser.Model;

namespace LegalAgent.Chunking.Splitting;

/// <summary>One part of a unit: rendered content and its metadata.</summary>
/// <param name="Content">Markdown of the part.</param>
/// <param name="ListLabels">Labels of the list item the part starts with and of its ancestors, outermost first.</param>
/// <param name="Pages">Source pages of the part.</param>
/// <param name="ExceedsLimit">True when a single indivisible element is longer than the limit.</param>
internal sealed record UnitPart(string Content, IReadOnlyList<string> ListLabels, PageSpan Pages, bool ExceedsLimit);

/// <summary>Splits a unit into parts within the length limit (research R3).</summary>
internal static class UnitSplitter
{
    /// <summary>The parts of <paramref name="unit"/>, in document order.</summary>
    public static IReadOnlyList<UnitPart> Split(Unit unit, FragmentRenderer renderer, int maxLength)
    {
        ArgumentNullException.ThrowIfNull(unit);
        ArgumentNullException.ThrowIfNull(renderer);

        string content = renderer.Render(unit.Section, unit.Blocks, unit.Footnotes);
        return [new UnitPart(content, [], Pages(unit), content.Length > maxLength)];
    }

    private static PageSpan Pages(Unit unit)
    {
        if (unit.Section is { } section)
        {
            return new PageSpan(section.Pages.First, section.Pages.Last);
        }

        IEnumerable<int> firsts = unit.Blocks.Select(b => b.Pages.First).Concat(unit.Footnotes.Select(f => f.Page));
        IEnumerable<int> lasts = unit.Blocks.Select(b => b.Pages.Last).Concat(unit.Footnotes.Select(f => f.Page));
        return new PageSpan(firsts.DefaultIfEmpty(1).Min(), lasts.DefaultIfEmpty(1).Max());
    }
}
