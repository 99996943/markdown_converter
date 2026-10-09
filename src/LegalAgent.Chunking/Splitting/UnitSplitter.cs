using LegalAgent.Chunking.Model;
using LegalAgent.PdfParser.Model;

namespace LegalAgent.Chunking.Splitting;

/// <summary>One part of a unit: rendered content and its metadata.</summary>
/// <param name="Content">Markdown of the part.</param>
/// <param name="ListLabels">Labels of the list item the part starts with and of its ancestors, outermost first.</param>
/// <param name="Pages">Source pages of the part.</param>
/// <param name="ExceedsLimit">True when a single indivisible element is longer than the limit.</param>
internal sealed record UnitPart(string Content, IReadOnlyList<string> ListLabels, PageSpan Pages, bool ExceedsLimit);

/// <summary>
/// Splits a unit into parts within the length limit (research R3). The unit's blocks are broken into atoms
/// (indivisible pieces) that are packed greedily in document order; the length of a candidate part is measured by
/// rendering it. A unit that fits is one part.
/// </summary>
internal static class UnitSplitter
{
    /// <summary>The parts of <paramref name="unit"/>, in document order.</summary>
    public static IReadOnlyList<UnitPart> Split(Unit unit, FragmentRenderer renderer, int maxLength)
    {
        ArgumentNullException.ThrowIfNull(unit);
        ArgumentNullException.ThrowIfNull(renderer);

        string whole = renderer.Render(unit.Section, unit.Blocks, unit.Footnotes);
        if (whole.Length <= maxLength)
        {
            return [new UnitPart(whole, [], Pages(unit), false)];
        }

        List<Atom> atoms = Atoms(unit.Blocks);
        var groups = new List<List<Atom>>();
        var current = new List<Atom>();
        foreach (Atom atom in atoms)
        {
            if (current.Count > 0 && Render(unit, renderer, [.. current, atom], last: false).Length > maxLength)
            {
                groups.Add(current);
                current = [];
            }

            current.Add(atom);
        }

        if (current.Count > 0 || groups.Count == 0)
        {
            groups.Add(current);
        }

        var parts = new List<UnitPart>(groups.Count);
        for (int g = 0; g < groups.Count; g++)
        {
            string content = Render(unit, renderer, groups[g], last: g == groups.Count - 1);
            parts.Add(new UnitPart(content, [], Pages(unit), content.Length > maxLength));
        }

        return parts;
    }

    private static string Render(Unit unit, FragmentRenderer renderer, IReadOnlyList<Atom> atoms, bool last) =>
        renderer.Render(unit.Section, Blocks(atoms), last ? unit.Footnotes : []);

    private static List<Atom> Atoms(IReadOnlyList<ContentBlock> blocks) => blocks.Select(b => new Atom(b)).ToList();

    private static List<ContentBlock> Blocks(IReadOnlyList<Atom> atoms) => atoms.Select(a => a.Block).ToList();

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

    /// <summary>An indivisible piece of a unit's content.</summary>
    private sealed record Atom(ContentBlock Block);
}
