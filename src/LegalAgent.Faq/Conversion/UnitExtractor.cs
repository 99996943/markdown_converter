using LegalAgent.Faq.Model;
using LegalAgent.PdfParser.Model;

namespace LegalAgent.Faq.Conversion;

/// <summary>Collects the units a model may cite from a parsed document.</summary>
public static class UnitExtractor
{
    /// <summary>Designations and heading texts of all sections (recursively), without duplicates, in document order.</summary>
    /// <param name="document">The parsed document.</param>
    /// <returns>The units.</returns>
    public static IReadOnlyList<string> FromDocument(LegalDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var units = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        Collect(document.Sections, units, seen);
        return units;
    }

    /// <summary>
    /// Where each unit of <see cref="FromDocument"/> starts, in the same order: the first page of its section and the
    /// anchor of the section heading in <paramref name="markdown"/>.
    /// </summary>
    /// <param name="document">The parsed document.</param>
    /// <param name="markdown">The Markdown rendered from <paramref name="document"/>.</param>
    /// <returns>One location per unit.</returns>
    public static IReadOnlyList<FaqUnitLocation> Locate(LegalDocument document, string markdown)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(markdown);
        Section[] sections = [.. PreOrder(document.Sections)];
        IReadOnlyList<string>? anchors = SectionAnchors(document, markdown, sections.Length);
        var locations = new List<FaqUnitLocation>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int k = 0; k < sections.Length; k++)
        {
            foreach (string? unit in new[] { sections[k].Designation, sections[k].HeadingText })
            {
                if (!string.IsNullOrWhiteSpace(unit) && seen.Add(unit))
                {
                    locations.Add(new FaqUnitLocation(unit, sections[k].Pages.First, anchors?[k]));
                }
            }
        }

        return locations;
    }

    /// <summary>
    /// Anchors of the section headings (all headings but the title) in rendering order; <c>null</c> when their number
    /// differs from the number of sections, so no anchor can point at a wrong heading.
    /// </summary>
    private static IReadOnlyList<string>? SectionAnchors(LegalDocument document, string markdown, int sectionCount)
    {
        IReadOnlyList<string> anchors = MarkdownAnchors.ForDocument(markdown);
        int skip = string.IsNullOrWhiteSpace(document.Title) ? 0 : 1;
        return anchors.Count - skip == sectionCount ? [.. anchors.Skip(skip)] : null;
    }

    /// <summary>Sections in rendering order: each section, then its subsections.</summary>
    private static IEnumerable<Section> PreOrder(IReadOnlyList<Section> sections)
    {
        foreach (Section section in sections)
        {
            yield return section;
            foreach (Section child in PreOrder(section.Children))
            {
                yield return child;
            }
        }
    }

    private static void Collect(IReadOnlyList<Section> sections, List<string> units, HashSet<string> seen)
    {
        foreach (Section section in sections)
        {
            Add(section.Designation, units, seen);
            Add(section.HeadingText, units, seen);
            Collect(section.Children, units, seen);
        }
    }

    private static void Add(string? unit, List<string> units, HashSet<string> seen)
    {
        if (!string.IsNullOrWhiteSpace(unit) && seen.Add(unit))
        {
            units.Add(unit);
        }
    }
}
