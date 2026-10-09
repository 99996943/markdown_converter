using LegalAgent.Corpus.Content;
using LegalAgent.Corpus.Planning;
using LegalAgent.Corpus.Tests.Unit.Content;

namespace LegalAgent.Corpus.Tests.Unit.Planning;

/// <summary>US3 (FR-120 – FR-122): versions, outdated documents and contradiction pairs in the plan.</summary>
public class VersionsPlannerTests
{
    private static readonly ContentLibrary Mini = MiniContent.Load();

    private static readonly RunParameters Defaults = new() { Seed = 42, DocumentsPerType = 10, Pages = new PageRange(5, 9) };

    private static CorpusPlan Plan(RunParameters? parameters = null) => CorpusPlanner.Plan(Mini, parameters ?? Defaults);

    private static List<DocumentPlan> Versions(CorpusPlan plan, DocumentPlan latest) =>
        plan.Documents.Where(d => d.Designation == latest.Designation).OrderBy(d => d.Version).ToList();

    [Fact]
    public void VersionedShare_OfTen_GivesThreeDocumentsPerTypeInTwoToMaxVersions()
    {
        CorpusPlan plan = Plan();

        foreach (string type in new[] { "regulaminy", "taryfy", "procedury" })
        {
            var latest = plan.Documents.Where(d => d.Type == type && !d.Id.Contains("-w", StringComparison.Ordinal)).ToList();
            Assert.Equal(10, latest.Count);
            var versioned = latest.Where(d => d.Version > 1).ToList();
            Assert.Equal(3, versioned.Count);
            Assert.All(versioned, d => Assert.InRange(d.Version, 2, Defaults.MaxVersions));
        }
    }

    [Fact]
    public void Versions_ShareDesignationTemplateAndContent_AndHaveContinuousDisjointPeriods()
    {
        CorpusPlan plan = Plan();

        foreach (DocumentPlan latest in plan.Documents.Where(d => d.Version > 1 && !d.Id.Contains("-w", StringComparison.Ordinal)))
        {
            List<DocumentPlan> versions = Versions(plan, latest);
            Assert.Equal(Enumerable.Range(1, latest.Version), versions.Select(v => v.Version));
            for (int k = 0; k < versions.Count; k++)
            {
                DocumentPlan v = versions[k];
                Assert.Equal(k == versions.Count - 1 ? latest.Id : $"{latest.Id}-w{k + 1}", v.Id);
                Assert.Equal(latest.Template, v.Template);
                Assert.Equal(latest.Layout, v.Layout);
                Assert.Equal(latest.BlockPool, v.BlockPool);
                Assert.Equal(latest.Seed, v.Seed);
                Assert.Equal(latest.TargetPages, v.TargetPages);
                Assert.Equal(k == 0 ? null : versions[k - 1].Id, v.PreviousVersionId);
                if (k < versions.Count - 1)
                {
                    Assert.Equal(DocumentStatus.Nieaktualny, v.Status);
                    Assert.Equal(versions[k + 1].ValidFrom, v.ValidTo!.Value.AddDays(1));
                    Assert.True(v.ValidFrom < v.ValidTo);
                }
            }

            Assert.Equal(DocumentStatus.Obowiazujacy, latest.Status);
            Assert.Null(latest.ValidTo);
        }
    }

    [Fact]
    public void EarlierVersions_ChangeAFactTheDocumentUses()
    {
        CorpusPlan plan = Plan();

        foreach (DocumentPlan latest in plan.Documents.Where(d => d.Version > 1 && !d.Id.Contains("-w", StringComparison.Ordinal)))
        {
            List<DocumentPlan> versions = Versions(plan, latest);
            for (int k = 0; k < versions.Count - 1; k++)
            {
                FactOverride change = Assert.Single(versions[k].FactOverrides, o => o.Reason == OverrideReason.Wersja);
                Assert.Contains(change.FactId, CorpusPlanner.GuaranteedFacts(Mini, Mini.Templates.Single(t => t.Id == latest.Template)));
                DocumentPlan next = versions[k + 1];
                FactValue following = next.FactOverrides.FirstOrDefault(o => o.FactId == change.FactId)?.Value
                    ?? Mini.Facts.ValueAt(change.FactId, next.ValidFrom);
                Assert.NotEqual(following, change.Value);
            }

            Assert.DoesNotContain(latest.FactOverrides, o => o.Reason == OverrideReason.Wersja);
        }
    }

    [Fact]
    public void OutdatedDocuments_EndBeforeTheReferenceDate_HaveNoSuccessor_AndAreNotVersioned()
    {
        CorpusPlan plan = Plan();

        foreach (string type in new[] { "regulaminy", "taryfy", "procedury" })
        {
            var outdated = plan.Documents
                .Where(d => d.Type == type && d.Version == 1 && d.PreviousVersionId is null && !d.Id.Contains("-w", StringComparison.Ordinal) && d.Status == DocumentStatus.Nieaktualny)
                .ToList();
            Assert.Equal(2, outdated.Count);
            Assert.All(outdated, d =>
            {
                Assert.True(d.ValidTo < Defaults.ReferenceDate);
                Assert.True(d.ValidFrom < d.ValidTo);
                Assert.DoesNotContain(plan.Documents, o => o.PreviousVersionId == d.Id);
                Assert.Empty(d.FactOverrides);
            });
        }
    }

    [Fact]
    public void ContradictionPairs_ShareAFactWithDifferentValues_InOverlappingPeriods()
    {
        CorpusPlan plan = Plan();
        var pairs = plan.Documents
            .SelectMany(d => d.Contradictions.Select(c => (Doc: d, Other: plan.Documents.Single(o => o.Id == c.With), c.FactId)))
            .Where(p => string.CompareOrdinal(p.Doc.Id, p.Other.Id) < 0)
            .ToList();

        Assert.Equal(4, pairs.Count);
        foreach (string type in new[] { "regulaminy", "taryfy", "procedury" })
        {
            Assert.Single(pairs, p => p.Doc.Type == type && p.Other.Type == type);
        }

        var cross = Assert.Single(pairs, p => p.Doc.Type != p.Other.Type);
        Assert.Equal(["regulaminy", "taryfy"], new[] { cross.Doc.Type, cross.Other.Type }.Order(StringComparer.Ordinal));

        foreach ((DocumentPlan a, DocumentPlan b, string fact) in pairs)
        {
            Assert.Contains(b.Contradictions, c => c.With == a.Id && c.FactId == fact);
            Assert.Equal(DocumentStatus.Obowiazujacy, a.Status);
            Assert.Equal(DocumentStatus.Obowiazujacy, b.Status);
            Assert.Null(a.ValidTo);
            Assert.Null(b.ValidTo);
            Assert.Contains(fact, CorpusPlanner.GuaranteedFacts(Mini, Mini.Templates.Single(t => t.Id == a.Template)));
            Assert.Contains(fact, CorpusPlanner.GuaranteedFacts(Mini, Mini.Templates.Single(t => t.Id == b.Template)));

            FactValue ValueOf(DocumentPlan d) =>
                d.FactOverrides.LastOrDefault(o => o.FactId == fact)?.Value ?? Mini.Facts.ValueAt(fact, d.ValidFrom);
            Assert.NotEqual(ValueOf(a), ValueOf(b));
            FactOverride planted = Assert.Single(new[] { a, b }.SelectMany(d => d.FactOverrides), o => o.Reason == OverrideReason.Sprzecznosc);
            Assert.Contains(planted.Value, Mini.Facts.Get(fact).Alternatives);
        }
    }

    [Fact]
    public void ZeroParameters_GiveOnlyBaseDocuments()
    {
        CorpusPlan plan = Plan(Defaults with { VersionedShare = 0, OutdatedPerType = 0, ContradictionPairsPerType = 0, CrossTypeContradictionPairs = 0 });

        Assert.Equal(30, plan.Documents.Count);
        Assert.All(plan.Documents, d =>
        {
            Assert.Equal(1, d.Version);
            Assert.Equal(DocumentStatus.Obowiazujacy, d.Status);
            Assert.Empty(d.FactOverrides);
            Assert.Empty(d.Contradictions);
        });
    }

    [Fact]
    public void Plan_IsDeterministic()
    {
        static string Dump(CorpusPlan p) => string.Join(
            "\n",
            p.Documents.Select(d => $"{d.Id}|{d.Version}|{d.ValidFrom}|{d.ValidTo}|{d.Status}|{d.PreviousVersionId}|"
                + string.Join(",", d.FactOverrides.Select(o => $"{o.FactId}={o.Value}/{o.Reason}")) + "|"
                + string.Join(",", d.Contradictions.Select(c => $"{c.With}:{c.FactId}"))));

        Assert.Equal(Dump(Plan()), Dump(Plan()));
    }
}
