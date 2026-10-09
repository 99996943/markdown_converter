using LegalAgent.Corpus.Composition;
using LegalAgent.Corpus.Content;
using LegalAgent.Corpus.Planning;
using LegalAgent.Corpus.Typesetting;

namespace LegalAgent.Corpus.Manifest;

/// <summary>
/// Changes against the previous version and contradictions with other documents (FR-141): each names the fact, the unit
/// and the page where this document first states it, and the values as the documents print them.
/// </summary>
public static class ManifestRelations
{
    /// <summary>The facts stated by both versions whose printed values differ, in fact id order.</summary>
    public static IReadOnlyList<VersionChange> Changes(ContentLibrary content, DocumentPlan next, FitResult nextFit, DocumentPlan previous, FitResult previousFit)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(nextFit);
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(previousFit);

        var changes = new List<VersionChange>();
        foreach ((string fact, IReadOnlyList<FactUse> uses) in nextFit.Composition.FactUses.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            if (!previousFit.Composition.FactUses.ContainsKey(fact))
            {
                continue;
            }

            string before = Printed(content, previous, fact);
            string after = Printed(content, next, fact);
            if (!string.Equals(before, after, StringComparison.Ordinal))
            {
                (string unit, int page) = Place(nextFit, uses[0]);
                changes.Add(new VersionChange(unit, page, fact, before, after));
            }
        }

        return changes;
    }

    /// <summary>The planted contradictions of <paramref name="doc"/>, with the values here and in the other document.</summary>
    /// <exception cref="CorpusGenerationException">The document does not state the fact of a planted contradiction.</exception>
    public static IReadOnlyList<Contradiction> Contradictions(ContentLibrary content, DocumentPlan doc, FitResult fit, IReadOnlyList<DocumentPlan> plan)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(doc);
        ArgumentNullException.ThrowIfNull(fit);
        ArgumentNullException.ThrowIfNull(plan);

        var result = new List<Contradiction>();
        foreach (PlannedContradiction planned in doc.Contradictions)
        {
            if (!fit.Composition.FactUses.TryGetValue(planned.FactId, out IReadOnlyList<FactUse>? uses) || uses.Count == 0)
            {
                throw new CorpusGenerationException($"Dokument {doc.Id} nie zawiera faktu '{planned.FactId}' sprzeczności z {planned.With}.") { DocumentId = doc.Id };
            }

            DocumentPlan other = plan.First(d => string.Equals(d.Id, planned.With, StringComparison.Ordinal));
            (string unit, int page) = Place(fit, uses[0]);
            result.Add(new Contradiction(planned.With, unit, page, planned.FactId, Printed(content, doc, planned.FactId), Printed(content, other, planned.FactId)));
        }

        return result;
    }

    /// <summary>
    /// The poison of a poisoned document (FR-142): kind, imitated document, the pattern's description and every place
    /// with its page, unit, kind of element, verbatim text and — for instructions to an AI — the goal.
    /// </summary>
    public static PoisonInfo Poison(ContentLibrary content, DocumentPlan doc, FitResult fit)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(doc);
        ArgumentNullException.ThrowIfNull(fit);
        PoisonPlan plan = doc.Poison ?? throw new ArgumentException("Dokument nie jest zatruty: " + doc.Id, nameof(doc));
        PoisonPattern pattern = content.PoisonPatterns.First(p => string.Equals(p.Id, plan.PatternId, StringComparison.Ordinal));
        var places = fit.Composition.Poison
            .Select(p =>
            {
                (string unit, int page) = p.ElementId switch
                {
                    "okladka" => ("okładka", 1),
                    "metryczka" => ("metryczka", fit.Typeset.ElementPages.TryGetValue("metryczka", out PageSpan card) ? card.First : 1),
                    _ => Place(fit, new FactUse(p.ElementId, null)),
                };
                return new PoisonPlace(page, unit, p.Element, p.Text, pattern.Goal);
            })
            .ToList();
        return new PoisonInfo(plan.Kind, plan.ImitatesId, pattern.Description, places);
    }

    /// <summary>The manifest form of an element unit: without the final period; a bare section number becomes „sekcja N”.</summary>
    public static string UnitLabel(string unit)
    {
        ArgumentNullException.ThrowIfNull(unit);
        string label = unit.TrimEnd('.');
        return label.Length > 0 && (label.All(char.IsDigit) || label.All(c => "IVXLC".Contains(c, StringComparison.Ordinal)))
            ? "sekcja " + label
            : label;
    }

    /// <summary>The value of <paramref name="fact"/> as <paramref name="doc"/> prints it.</summary>
    private static string Printed(ContentLibrary content, DocumentPlan doc, string fact)
    {
        FactValue value = doc.FactOverrides.LastOrDefault(o => string.Equals(o.FactId, fact, StringComparison.Ordinal))?.Value
            ?? content.Facts.ValueAt(fact, doc.ValidFrom);
        return PolishFormat.Format(content.Facts.Get(fact).Kind, value);
    }

    /// <summary>
    /// Unit and page of a fact use: the tariff position, the element's unit, or the unit of the nearest element above
    /// that has one; the front matter is „metryczka” on page 1.
    /// </summary>
    private static (string Unit, int Page) Place(FitResult fit, FactUse use)
    {
        if (use.ElementId.Length == 0)
        {
            return ("metryczka", 1);
        }

        IReadOnlyList<Element> elements = fit.Composition.Document.Elements;
        int index = -1;
        for (int i = 0; i < elements.Count; i++)
        {
            if (string.Equals(elements[i].Id, use.ElementId, StringComparison.Ordinal))
            {
                index = i;
                break;
            }
        }

        string? unit = use.Unit;
        for (int i = index; unit is null && i >= 0; i--)
        {
            unit = elements[i].Unit;
        }

        int page = fit.Typeset.ElementPages.TryGetValue(use.ElementId, out PageSpan span) ? span.First : 1;
        return (UnitLabel(unit ?? "metryczka"), page);
    }
}
