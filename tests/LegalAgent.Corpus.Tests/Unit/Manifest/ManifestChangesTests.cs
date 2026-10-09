using System.Text.RegularExpressions;
using LegalAgent.Corpus.Content;
using LegalAgent.Corpus.Manifest;
using LegalAgent.Corpus.Planning;
using LegalAgent.Corpus.Tests.Unit.Content;
using LegalAgent.Corpus.Typesetting;

namespace LegalAgent.Corpus.Tests.Unit.Manifest;

/// <summary>US3 (FR-141, FR-123): changes against the previous version and contradictions in the manifest.</summary>
public partial class ManifestChangesTests
{
    private static readonly ContentLibrary Mini = MiniContent.Load();

    private static readonly RunParameters Parameters = new() { Seed = 42, DocumentsPerType = 10, Pages = new PageRange(1, 9) };

    private static readonly CorpusPlan Plan = CorpusPlanner.Plan(Mini, Parameters);

    private static FitResult Fit(DocumentPlan doc) => PageFitter.Fit(doc, Mini, Parameters.Seed, Parameters.Pages);

    private static DocumentPlan Doc(string id) => Plan.Documents.Single(d => d.Id == id);

    private static string Value(DocumentPlan doc, string fact) =>
        PolishFormat.Format(
            Mini.Facts.Get(fact).Kind,
            doc.FactOverrides.LastOrDefault(o => o.FactId == fact)?.Value ?? Mini.Facts.ValueAt(fact, doc.ValidFrom));

    [GeneratedRegex(@"^(§ \d+( ust\. \d+)?( pkt \d+)?( lit\. [a-z])?|poz\. \d+(\.\d+)*|krok \d+(\.\d+)*|metryczka|Rozdział \d+|\d+(\.\d+)*|Załącznik nr \d+)$")]
    private static partial Regex UnitFormat();

    [Fact]
    public void Changes_ListTheChangedFact_WithUnitPageAndBothValues()
    {
        foreach (DocumentPlan next in Plan.Documents.Where(d => d.PreviousVersionId is not null))
        {
            DocumentPlan previous = Doc(next.PreviousVersionId!);
            FitResult nextFit = Fit(next);
            IReadOnlyList<VersionChange> changes = ManifestRelations.Changes(Mini, next, nextFit, previous, Fit(previous));

            string fact = Assert.Single(previous.FactOverrides, o => o.Reason == OverrideReason.Wersja).FactId;
            VersionChange change = Assert.Single(changes, c => c.Fact == fact);
            Assert.Equal(Value(previous, fact), change.Before);
            Assert.Equal(Value(next, fact), change.After);
            Assert.NotEqual(change.Before, change.After);
            Assert.InRange(change.Page, 1, nextFit.Typeset.PageCount);
            Assert.Matches(UnitFormat(), change.Unit);
            Assert.All(changes, c => Assert.NotEqual(c.Before, c.After));
        }
    }

    [Fact]
    public void Changes_OfATariffFact_NameThePosition()
    {
        DocumentPlan next = Plan.Documents.First(d => d.Type == "taryfy" && d.PreviousVersionId is not null);
        DocumentPlan previous = Doc(next.PreviousVersionId!);

        IReadOnlyList<VersionChange> changes = ManifestRelations.Changes(Mini, next, Fit(next), previous, Fit(previous));

        Assert.Contains(changes, c => c.Unit.StartsWith("poz. ", StringComparison.Ordinal));
    }

    [Fact]
    public void Contradictions_NameTheOtherDocument_AndBothValues()
    {
        foreach (DocumentPlan doc in Plan.Documents.Where(d => d.Contradictions.Count > 0))
        {
            FitResult fit = Fit(doc);
            IReadOnlyList<Contradiction> contradictions = ManifestRelations.Contradictions(Mini, doc, fit, Plan.Documents);

            Assert.Equal(doc.Contradictions.Count, contradictions.Count);
            foreach (PlannedContradiction planned in doc.Contradictions)
            {
                Contradiction c = Assert.Single(contradictions, c => c.With == planned.With && c.Fact == planned.FactId);
                Assert.Equal(Value(doc, planned.FactId), c.This);
                Assert.Equal(Value(Doc(planned.With), planned.FactId), c.Other);
                Assert.NotEqual(c.This, c.Other);
                Assert.InRange(c.Page, 1, fit.Typeset.PageCount);
                Assert.Matches(UnitFormat(), c.Unit);
            }
        }
    }

    [Fact]
    public void DocumentsWithStates_CarryNoMetaRemarks()
    {
        foreach (DocumentPlan doc in Plan.Documents.Where(d => d.Status == DocumentStatus.Nieaktualny || d.Contradictions.Count > 0 || d.Version > 1))
        {
            string text = string.Join(" ", Fit(doc).Typeset.Truth.Words).ToLowerInvariant();
            Assert.DoesNotContain("sprzeczn", text, StringComparison.Ordinal);
            Assert.DoesNotContain("nieaktualn", text, StringComparison.Ordinal);
        }
    }
}
