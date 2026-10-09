using System.Globalization;
using LegalAgent.Corpus.Content;
using LegalAgent.Corpus.Random;

namespace LegalAgent.Corpus.Planning;

/// <summary>
/// US4 (FR-130 – FR-132): plans the poisoned documents. A poisoned document is a copy of the plan of a document it
/// imitates (same template, layout, pool, seed, designation and dates) plus one poison pattern applied at one place.
/// </summary>
/// <remarks>
/// For the n-th document of a (kind, type) pair the planner prefers a placement not used yet for the pair, then one of
/// the class <c>(n − 1) mod 3</c> — a footnote or table cell, the record card or cover, a paragraph or callout — so that hidden and explicit variants are spread over the documents (FR-132), and patterns
/// whose goal has not been used yet, so
/// that every goal of <c>polecenia-dla-ai</c> occurs (FR-132a). Ties are broken by streams derived from the seed.
/// </remarks>
internal static class PoisonPlanner
{
    private static readonly string[][] PlacementClasses = [["przypis", "komorka-tabeli"], ["metryczka", "okladka"], ["akapit", "ramka"]];

    /// <summary>The poisoned documents for the quotas of <paramref name="parameters"/>, in quota, type and number order.</summary>
    /// <exception cref="RunParametersException">A kind is unknown or has no pattern for a type of the run.</exception>
    public static List<DocumentPlan> Plan(ContentLibrary content, RunParameters parameters, IReadOnlyList<DocumentPlan> documents)
    {
        var result = new List<DocumentPlan>();
        var usedGoals = new HashSet<string>(StringComparer.Ordinal);
        var types = content.Types.Where(t => documents.Any(d => string.Equals(d.Type, t.Id, StringComparison.Ordinal))).ToList();
        foreach (PoisonQuota quota in parameters.Poison)
        {
            PoisonKindDef kind = content.PoisonKinds.FirstOrDefault(k => string.Equals(k.Kind, quota.Kind, StringComparison.Ordinal))
                ?? throw new RunParametersException("poison", $"nieznany rodzaj zatrucia '{quota.Kind}' (brak pliku zatrucia/{quota.Kind}.yaml)");
            if (quota.PerType == 0)
            {
                continue;
            }

            foreach (DocumentTypeDef type in types)
            {
                var patterns = content.PoisonPatterns
                    .Where(p => string.Equals(p.Kind, kind.Kind, StringComparison.Ordinal) && p.Types.Contains(type.Id, StringComparer.Ordinal))
                    .OrderBy(p => p.Id, StringComparer.Ordinal)
                    .ToList();
                if (patterns.Count == 0)
                {
                    throw new RunParametersException("poison", $"rodzaj zatrucia '{kind.Kind}' nie ma wzorców dla typu '{type.Id}'");
                }

                var originals = documents
                    .Where(d => string.Equals(d.Type, type.Id, StringComparison.Ordinal) && d.Poison is null && d.Id.IndexOf("-w", StringComparison.Ordinal) < 0)
                    .ToList();
                int width = Math.Max(2, quota.PerType.ToString(CultureInfo.InvariantCulture).Length);
                var usedPlacements = new HashSet<string>(StringComparer.Ordinal);
                for (int n = 1; n <= quota.PerType; n++)
                {
                    string id = $"ZAT-{type.Prefix}-{kind.Abbreviation}-{n.ToString(CultureInfo.InvariantCulture).PadLeft(width, '0')}";
                    DocumentPlan poisoned = Poisoned(content, parameters.Seed, kind, patterns, originals, n, id, usedGoals, usedPlacements);
                    usedPlacements.Add(poisoned.Poison!.Placement);
                    result.Add(poisoned);
                }
            }
        }

        return result;
    }

    private static DocumentPlan Poisoned(
        ContentLibrary content,
        ulong seed,
        PoisonKindDef kind,
        List<PoisonPattern> patterns,
        List<DocumentPlan> originals,
        int n,
        string id,
        HashSet<string> usedGoals,
        HashSet<string> usedPlacements)
    {
        string[] wanted = PlacementClasses[(n - 1) % PlacementClasses.Length];
        var shuffledPatterns = new List<PoisonPattern>(patterns);
        DeterministicRandom.Derive(seed, "zatrucie-wzorzec", id).Shuffle(shuffledPatterns);
        var shuffledDocs = new List<DocumentPlan>(originals);
        DeterministicRandom.Derive(seed, "zatrucie-dokument", id).Shuffle(shuffledDocs);

        (PoisonPattern Pattern, string Placement, DocumentPlan Doc)? best = null;
        int bestScore = -1;
        foreach (PoisonPattern pattern in shuffledPatterns)
        {
            IEnumerable<string> placements = pattern.Placements.Count == 0
                ? [string.Empty]
                : pattern.Placements.OrderBy(p => wanted.Contains(p, StringComparer.Ordinal) ? 0 : 1).ThenBy(p => p, StringComparer.Ordinal);
            foreach (string placement in placements)
            {
                foreach (DocumentPlan doc in shuffledDocs)
                {
                    if (!Feasible(content, pattern, placement, doc))
                    {
                        continue;
                    }

                    int score = (!usedPlacements.Contains(placement) ? 4 : 0)
                        + (placement.Length == 0 || wanted.Contains(placement, StringComparer.Ordinal) ? 2 : 0)
                        + (pattern.Goal is { } goal && !usedGoals.Contains(goal) ? 1 : 0);
                    if (score > bestScore)
                    {
                        best = (pattern, placement, doc);
                        bestScore = score;
                    }

                    break;
                }
            }
        }

        if (best is not { } chosen)
        {
            throw new CorpusGenerationException($"{id}: żaden dokument typu nie pozwala zastosować wzorców rodzaju '{kind.Kind}'.") { DocumentId = id };
        }

        if (chosen.Pattern.Goal is { } used)
        {
            usedGoals.Add(used);
        }

        int variant = chosen.Pattern.TextVariants.Count == 0 ? 0 : DeterministicRandom.Derive(seed, "zatrucie-wariant", id).Next(chosen.Pattern.TextVariants.Count);
        DocumentPlan original = chosen.Doc;
        var overrides = new List<FactOverride>(original.FactOverrides);
        if (string.Equals(chosen.Pattern.Operation, "nadpisz-fakt", StringComparison.Ordinal) && chosen.Pattern.Fact is { } fact)
        {
            FactValue current = original.FactOverrides.LastOrDefault(o => string.Equals(o.FactId, fact, StringComparison.Ordinal))?.Value
                ?? content.Facts.ValueAt(fact, original.ValidFrom);
            FactValue value = chosen.Pattern.Value ?? content.Facts.Get(fact).Alternatives.First(v => v != current);
            overrides.Add(new FactOverride(fact, value, OverrideReason.Zatrucie));
        }

        return original with
        {
            Id = id,
            PreviousVersionId = null,
            Contradictions = [],
            FactOverrides = overrides,
            Poison = new PoisonPlan(kind.Kind, kind.Abbreviation, chosen.Pattern.Id, chosen.Placement, original.Id, variant),
        };
    }

    /// <summary>Whether <paramref name="pattern"/> can be applied at <paramref name="placement"/> of <paramref name="doc"/>.</summary>
    private static bool Feasible(ContentLibrary content, PoisonPattern pattern, string placement, DocumentPlan doc)
    {
        DocumentTemplate template = content.Templates.First(t => string.Equals(t.Id, doc.Template, StringComparison.Ordinal));
        bool placed = placement switch
        {
            "metryczka" => template.Front.RecordCard,
            "okladka" => template.Front.Cover,
            "komorka-tabeli" => HasTable(content, template),
            _ => true,
        };
        return placed && pattern.Operation switch
        {
            "nadpisz-fakt" => pattern.Fact is { } fact && DocumentStates.GuaranteedFacts(content, template).Contains(fact, StringComparer.Ordinal),
            "zmien-czolo" => template.Front.RecordCard || template.Front.Cover,
            "przesun-daty" => doc.Status == DocumentStatus.Nieaktualny && doc.ValidTo is not null,
            _ => true,
        };
    }

    /// <summary>A required block of the template has a table or tariff positions.</summary>
    private static bool HasTable(ContentLibrary content, DocumentTemplate template) =>
        template.Sections.SelectMany(s => s.Required)
            .Select(id => content.Blocks.FirstOrDefault(b => string.Equals(b.Id, id, StringComparison.Ordinal)))
            .Any(b => b is not null && b.Elements.Any(e => e is SourceTable or SourceTariffItems));
}
