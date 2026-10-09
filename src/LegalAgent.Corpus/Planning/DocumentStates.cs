using System.Globalization;
using System.Text.RegularExpressions;
using LegalAgent.Corpus.Content;
using LegalAgent.Corpus.Random;

namespace LegalAgent.Corpus.Planning;

/// <summary>
/// US3 (FR-120 – FR-122): turns the base documents of the plan into versions, outdated documents and contradiction
/// pairs. Everything is drawn from streams derived from the master seed, so the plan stays deterministic.
/// </summary>
/// <remarks>
/// Per type the base documents are shuffled once: the first <see cref="RunParameters.VersionedCount"/> get 2 –
/// <see cref="RunParameters.MaxVersions"/> versions, the next <see cref="RunParameters.OutdatedPerType"/> become
/// outdated, the rest stay current and supply the contradiction pairs. Versions share the template, layout, block pool
/// and seed (identical text) and differ in one fact the template certainly states (<see cref="GuaranteedFacts"/>);
/// a contradiction pair states one such fact with a value from its <c>alternatywy</c> in the second document.
/// </remarks>
internal static partial class DocumentStates
{
    private static readonly FactKind[] PreferredKinds = [FactKind.Kwota, FactKind.Procent, FactKind.Termin];

    /// <summary>Facts stated by the required blocks of <paramref name="template"/> outside variant groups, ordinal order.</summary>
    public static IReadOnlyList<string> GuaranteedFacts(ContentLibrary content, DocumentTemplate template)
    {
        var facts = new SortedSet<string>(StringComparer.Ordinal);
        foreach (string id in template.Sections.SelectMany(s => s.Required))
        {
            ContentBlock? block = content.Blocks.FirstOrDefault(b => string.Equals(b.Id, id, StringComparison.Ordinal));
            if (block is null)
            {
                continue;
            }

            foreach (string text in Texts(block))
            {
                facts.UnionWith(FactsOutsideVariants(text));
            }
        }

        return [.. facts];
    }

    /// <summary>Applies versions, outdated documents and contradiction pairs to the base documents of every type.</summary>
    public static List<DocumentPlan> Apply(ContentLibrary content, RunParameters parameters, IReadOnlyList<List<DocumentPlan>> types)
    {
        ulong seed = parameters.Seed;
        var current = new List<List<DocumentPlan>>();
        var result = new List<List<DocumentPlan>>();
        foreach (List<DocumentPlan> docs in types)
        {
            if (docs.Count == 0)
            {
                result.Add([]);
                current.Add([]);
                continue;
            }

            string type = docs[0].Type;
            var order = Enumerable.Range(0, docs.Count).ToList();
            DeterministicRandom.Derive(seed, "stany", type).Shuffle(order);
            var plans = new List<DocumentPlan>(docs);
            var rest = new List<int>();
            for (int k = 0; k < order.Count; k++)
            {
                int i = order[k];
                if (k < parameters.VersionedCount)
                {
                    continue;
                }

                if (k < parameters.VersionedCount + parameters.OutdatedPerType)
                {
                    plans[i] = Outdated(seed, parameters.ReferenceDate, docs[i]);
                }
                else
                {
                    rest.Add(i);
                }
            }

            var all = new List<DocumentPlan>();
            for (int k = 0; k < Math.Min(parameters.VersionedCount, order.Count); k++)
            {
                all.AddRange(Versions(content, parameters, docs[order[k]]));
            }

            foreach (int i in Enumerable.Range(0, docs.Count).Where(i => !order.Take(parameters.VersionedCount).Contains(i)))
            {
                all.Add(plans[i]);
            }

            result.Add(all);
            current.Add(rest.Order().Select(i => docs[i]).ToList());
        }

        var pairs = new List<(DocumentPlan A, DocumentPlan B, string Fact)>();
        var used = new HashSet<string>(StringComparer.Ordinal);
        for (int t = 0; t < current.Count; t++)
        {
            for (int p = 0; p < parameters.ContradictionPairsPerType; p++)
            {
                pairs.Add(Pair(content, seed, current[t], current[t], used, $"{TypeOf(current[t])}/{p}")
                    ?? throw new CorpusGenerationException(
                        $"Typ '{TypeOf(current[t])}': brak pary dokumentów obowiązujących ze wspólnym faktem z alternatywami (sprzeczność {p + 1})."));
            }
        }

        var typePairs = new List<(int A, int B)>();
        for (int a = 0; a < current.Count; a++)
        {
            for (int b = a + 1; b < current.Count; b++)
            {
                typePairs.Add((a, b));
            }
        }

        for (int p = 0; p < parameters.CrossTypeContradictionPairs; p++)
        {
            (DocumentPlan, DocumentPlan, string)? found = null;
            for (int k = 0; k < typePairs.Count && found is null; k++)
            {
                (int a, int b) = typePairs[(p + k) % typePairs.Count];
                found = Pair(content, seed, current[a], current[b], used, $"*/{p}");
            }

            pairs.Add(found ?? throw new CorpusGenerationException(
                $"Brak pary dokumentów różnych typów ze wspólnym faktem z alternatywami (sprzeczność między typami {p + 1})."));
        }

        var byId = result.SelectMany(l => l).ToDictionary(d => d.Id, StringComparer.Ordinal);
        foreach ((DocumentPlan a, DocumentPlan b, string fact) in pairs)
        {
            FactValue here = ValueOf(content, byId[a.Id], fact);
            FactValue planted = content.Facts.Get(fact).Alternatives.First(v => v != here);
            byId[a.Id] = byId[a.Id] with { Contradictions = [.. byId[a.Id].Contradictions, new PlannedContradiction(b.Id, fact)] };
            byId[b.Id] = byId[b.Id] with
            {
                Contradictions = [.. byId[b.Id].Contradictions, new PlannedContradiction(a.Id, fact)],
                FactOverrides = [.. byId[b.Id].FactOverrides, new FactOverride(fact, planted, OverrideReason.Sprzecznosc)],
            };
        }

        return result
            .SelectMany(l => l.Select(d => byId[d.Id]).OrderBy(d => d.Id, StringComparer.Ordinal))
            .ToList();
    }

    private static string TypeOf(List<DocumentPlan> docs) => docs.Count > 0 ? docs[0].Type : "?";

    private static FactValue ValueOf(ContentLibrary content, DocumentPlan doc, string fact) =>
        doc.FactOverrides.LastOrDefault(o => string.Equals(o.FactId, fact, StringComparison.Ordinal))?.Value
            ?? content.Facts.ValueAt(fact, doc.ValidFrom);

    /// <summary>Two documents not yet in a pair (one from each list) sharing a guaranteed fact with alternatives.</summary>
    private static (DocumentPlan A, DocumentPlan B, string Fact)? Pair(
        ContentLibrary content,
        ulong seed,
        List<DocumentPlan> left,
        List<DocumentPlan> right,
        HashSet<string> used,
        string purpose)
    {
        var candidates = new List<(DocumentPlan A, DocumentPlan B)>();
        foreach (DocumentPlan a in left.Where(d => !used.Contains(d.Id)))
        {
            foreach (DocumentPlan b in right.Where(d => !used.Contains(d.Id) && !string.Equals(d.Id, a.Id, StringComparison.Ordinal)))
            {
                if (ReferenceEquals(left, right) && string.CompareOrdinal(a.Id, b.Id) > 0)
                {
                    continue;
                }

                candidates.Add((a, b));
            }
        }

        DeterministicRandom.Derive(seed, "sprzecznosci", purpose).Shuffle(candidates);
        foreach ((DocumentPlan a, DocumentPlan b) in candidates)
        {
            var shared = Changeable(content, a).Intersect(Changeable(content, b), StringComparer.Ordinal).ToList();
            if (shared.Count == 0)
            {
                continue;
            }

            string fact = Prefer(content, shared)[DeterministicRandom.Derive(seed, "sprzecznosc-fakt", a.Id + "/" + b.Id).Next(Prefer(content, shared).Count)];
            used.Add(a.Id);
            used.Add(b.Id);
            return string.CompareOrdinal(a.Id, b.Id) < 0 ? (a, b, fact) : (b, a, fact);
        }

        return null;
    }

    /// <summary>Guaranteed facts of the document's template that have alternative values.</summary>
    private static List<string> Changeable(ContentLibrary content, DocumentPlan doc) =>
        GuaranteedFacts(content, content.Templates.First(t => string.Equals(t.Id, doc.Template, StringComparison.Ordinal)))
            .Where(f => content.Facts.Contains(f) && content.Facts.Get(f).Alternatives.Count > 0)
            .ToList();

    /// <summary>Amounts, percentages and terms when there are any (a rate or a deadline), otherwise all.</summary>
    private static List<string> Prefer(ContentLibrary content, List<string> facts)
    {
        var preferred = facts.Where(f => PreferredKinds.Contains(content.Facts.Get(f).Kind)).ToList();
        return preferred.Count > 0 ? preferred : facts;
    }

    private static DocumentPlan Outdated(ulong seed, DateOnly reference, DocumentPlan doc)
    {
        DeterministicRandom random = DeterministicRandom.Derive(seed, "nieaktualny", doc.Id);
        DateOnly to = FirstOfMonth(reference.AddMonths(-(1 + random.Next(6)))).AddDays(-1);
        DateOnly from = FirstOfMonth(to.AddMonths(-(12 + random.Next(13))));
        return doc with { ValidFrom = from, ValidTo = to, Status = DocumentStatus.Nieaktualny };
    }

    private static List<DocumentPlan> Versions(ContentLibrary content, RunParameters parameters, DocumentPlan latest)
    {
        ulong seed = parameters.Seed;
        int count = 2 + DeterministicRandom.Derive(seed, "wersje", latest.Id).Next(parameters.MaxVersions - 1);
        List<string> facts = Prefer(content, Changeable(content, latest));
        if (facts.Count == 0)
        {
            throw new CorpusGenerationException(
                $"Dokument {latest.Id} (szablon '{latest.Template}'): brak faktu z alternatywami w blokach wymaganych — nie można utworzyć wersji.");
        }

        string fact = facts[DeterministicRandom.Derive(seed, "wersje-fakt", latest.Id).Next(facts.Count)];
        Fact definition = content.Facts.Get(fact);
        var versions = new DocumentPlan[count];
        versions[count - 1] = latest with { Version = count, PreviousVersionId = Id(latest.Id, count - 1) };
        FactValue next = content.Facts.ValueAt(fact, latest.ValidFrom);
        for (int k = count - 1; k >= 1; k--)
        {
            DocumentPlan following = versions[k];
            int months = 6 + DeterministicRandom.Derive(seed, "wersje-okres", Id(latest.Id, k)).Next(13);
            DateOnly from = FirstOfMonth(following.ValidFrom.AddMonths(-months));
            DateOnly to = following.ValidFrom.AddDays(-1);

            // The value then: the dated value in force at the start of this version, or else another known value.
            FactValue dated = content.Facts.ValueAt(fact, from);
            FactValue value = dated != next
                ? dated
                : definition.Values.Select(v => v.Value).Concat(definition.Alternatives).First(v => v != next);
            versions[k - 1] = latest with
            {
                Id = Id(latest.Id, k),
                Version = k,
                ValidFrom = from,
                ValidTo = to,
                Status = DocumentStatus.Nieaktualny,
                PreviousVersionId = k > 1 ? Id(latest.Id, k - 1) : null,
                FactOverrides = [new FactOverride(fact, value, OverrideReason.Wersja)],
            };
            next = value;
        }

        for (int k = 1; k < count; k++)
        {
            versions[k] = versions[k] with { EarlierVersionStarts = versions.Take(k).Select(v => v.ValidFrom).ToList() };
        }

        return [.. versions];
    }

    private static string Id(string latest, int version) => latest + "-w" + version.ToString(CultureInfo.InvariantCulture);

    private static DateOnly FirstOfMonth(DateOnly date) => new(date.Year, date.Month, 1);

    private static IEnumerable<string> FactsOutsideVariants(string text)
    {
        // Placeholders first ({{…}} may sit inside a variant), then variant groups {a|b} innermost-first.
        var facts = new List<string>();
        string masked = Placeholder().Replace(text, m =>
        {
            facts.Add(m.Groups["fact"].Success ? m.Groups["fact"].Value : string.Empty);
            return "\u0001" + (facts.Count - 1).ToString(CultureInfo.InvariantCulture) + "\u0002";
        });
        string previous;
        do
        {
            previous = masked;
            masked = VariantGroup().Replace(masked, string.Empty);
        }
        while (!string.Equals(previous, masked, StringComparison.Ordinal));

        foreach (Match m in Sentinel().Matches(masked))
        {
            string fact = facts[int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)];
            if (fact.Length > 0)
            {
                yield return fact;
            }
        }
    }

    private static IEnumerable<string> Texts(ContentBlock block)
    {
        foreach (SourceElement element in block.Elements)
        {
            switch (element)
            {
                case SourceParagraph p:
                    yield return p.Text;
                    break;
                case SourceClause c:
                    yield return c.Text;
                    foreach (SourcePoint point in c.Points)
                    {
                        yield return point.Text;
                        foreach (string letter in point.Letters)
                        {
                            yield return letter;
                        }
                    }

                    break;
                case SourceHeading h:
                    yield return h.Text;
                    break;
                case SourceTable t:
                    foreach (string cell in t.Rows.SelectMany(r => r).Concat(t.Notes))
                    {
                        yield return cell;
                    }

                    break;
                case SourceTariffItems items:
                    foreach (string s in TariffTexts(items.Items))
                    {
                        yield return s;
                    }

                    break;
                case SourceSteps steps:
                    foreach (string s in StepTexts(steps.Steps))
                    {
                        yield return s;
                    }

                    break;
                case SourceScheme scheme:
                    foreach (SourceSchemeStep step in scheme.Steps)
                    {
                        yield return step.Name;
                        foreach (string s in step.Explanation)
                        {
                            yield return s;
                        }
                    }

                    break;
                case SourceChecklist list:
                    foreach (string s in list.Items)
                    {
                        yield return s;
                    }

                    break;
                case SourceCallout callout:
                    yield return callout.Text;
                    break;
                case SourceRecordCard card:
                    foreach (KeyValuePair<string, string> pair in card.Pairs)
                    {
                        yield return pair.Value;
                    }

                    break;
            }
        }
    }

    private static IEnumerable<string> TariffTexts(IReadOnlyList<SourceTariffItem> items) =>
        items.SelectMany(i => new[] { i.Service, i.Mode, i.Rate }.Concat(TariffTexts(i.Children)));

    private static IEnumerable<string> StepTexts(IReadOnlyList<SourceStep> steps) =>
        steps.SelectMany(s => new[] { s.Text }.Concat(StepTexts(s.Children)));

    [GeneratedRegex(@"\{\{(?:fakt:(?<fact>[^}|]+)[^}]*|[^}]*)\}\}", RegexOptions.CultureInvariant)]
    private static partial Regex Placeholder();

    [GeneratedRegex(@"\{[^{}]*\}", RegexOptions.CultureInvariant)]
    private static partial Regex VariantGroup();

    [GeneratedRegex("\u0001(\\d+)\u0002", RegexOptions.CultureInvariant)]
    private static partial Regex Sentinel();
}
