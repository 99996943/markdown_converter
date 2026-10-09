using LegalAgent.Corpus.Content;
using LegalAgent.Corpus.Planning;
using LegalAgent.Corpus.Tests.Unit.Content;

namespace LegalAgent.Corpus.Tests.Unit.Planning;

/// <summary>US4 (FR-130, FR-131): poisoned documents in the plan.</summary>
public class PoisonPlannerTests
{
    private static readonly ContentLibrary Mini = MiniContent.Load();

    private static RunParameters Parameters(int perType = 2, IReadOnlyList<string>? types = null) => new()
    {
        Seed = 42,
        DocumentsPerType = 4,
        Types = types ?? ["regulaminy", "taryfy"],
        Pages = new PageRange(1, 9),
        VersionedShare = 0,
        OutdatedPerType = 1,
        ContradictionPairsPerType = 0,
        CrossTypeContradictionPairs = 0,
        Poison = [new PoisonQuota("polecenia-dla-ai", perType)],
    };

    [Fact]
    public void Quota_GivesPoisonedCopiesOfDocumentsOfTheSameType()
    {
        CorpusPlan plan = CorpusPlanner.Plan(Mini, Parameters());

        var poisoned = plan.Documents.Where(d => d.Poison is not null).ToList();
        Assert.Equal(["ZAT-REG-POL-01", "ZAT-REG-POL-02", "ZAT-TAR-POL-01", "ZAT-TAR-POL-02"], poisoned.Select(d => d.Id));
        foreach (DocumentPlan doc in poisoned)
        {
            PoisonPlan poison = doc.Poison!;
            DocumentPlan imitated = Assert.Single(plan.Documents, d => d.Id == poison.ImitatesId);
            Assert.Null(imitated.Poison);
            Assert.Equal(imitated.Type, doc.Type);
            Assert.Equal(imitated.Prefix, doc.Prefix);
            Assert.Equal(imitated.Designation, doc.Designation);
            Assert.Equal(imitated.Template, doc.Template);
            Assert.Equal(imitated.Layout, doc.Layout);
            Assert.Equal(imitated.BlockPool, doc.BlockPool);
            Assert.Equal(imitated.TargetPages, doc.TargetPages);
            Assert.Equal(imitated.Seed, doc.Seed);
            Assert.Equal(imitated.Version, doc.Version);
            Assert.Equal(imitated.ValidFrom, doc.ValidFrom);
            Assert.Equal(imitated.ValidTo, doc.ValidTo);
            Assert.Equal(imitated.Status, doc.Status);
            Assert.Equal("polecenia-dla-ai", poison.Kind);
            Assert.Equal("POL", poison.Abbreviation);
            PoisonPattern pattern = Assert.Single(Mini.PoisonPatterns, p => p.Id == poison.PatternId);
            Assert.Contains(doc.Type, pattern.Types);
            Assert.Contains(poison.Placement, pattern.Placements);
            Assert.InRange(poison.Variant, 0, pattern.TextVariants.Count - 1);
        }
    }

    [Fact]
    public void Placements_RotateOverTheDocumentsOfAType()
    {
        CorpusPlan plan = CorpusPlanner.Plan(Mini, Parameters(perType: 3));

        // Mini rules have no table, so only a footnote and a paragraph fit them; tariffs take all three placements.
        foreach ((string prefix, int distinct) in new[] { ("ZAT-REG-", 2), ("ZAT-TAR-", 3) })
        {
            var placements = plan.Documents.Where(d => d.Id.StartsWith(prefix, StringComparison.Ordinal)).Select(d => d.Poison!.Placement).ToList();
            Assert.Equal(3, placements.Count);
            Assert.Equal(distinct, placements.Distinct(StringComparer.Ordinal).Count());
        }
    }

    [Fact]
    public void KindWithoutPatternsForAType_IsAParameterError()
    {
        var error = Assert.Throws<RunParametersException>(() => CorpusPlanner.Plan(Mini, Parameters(types: ["procedury"])));

        Assert.Contains("polecenia-dla-ai", error.Message, StringComparison.Ordinal);
        Assert.Contains("procedury", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownKind_IsAParameterError()
    {
        RunParameters parameters = Parameters() with { Poison = [new PoisonQuota("nieznany-rodzaj", 1)] };

        var error = Assert.Throws<RunParametersException>(() => CorpusPlanner.Plan(Mini, parameters));

        Assert.Contains("nieznany-rodzaj", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ZeroQuota_AddsNoDocuments()
    {
        CorpusPlan plan = CorpusPlanner.Plan(Mini, Parameters(perType: 0));

        Assert.DoesNotContain(plan.Documents, d => d.Poison is not null);
    }

    [Fact]
    public void PoisonedDocuments_ComeAfterTheBaseDocuments_AndAreDeterministic()
    {
        CorpusPlan first = CorpusPlanner.Plan(Mini, Parameters());
        CorpusPlan second = CorpusPlanner.Plan(Mini, Parameters());

        int firstPoisoned = first.Documents.ToList().FindIndex(d => d.Poison is not null);
        Assert.All(first.Documents.Skip(firstPoisoned), d => Assert.NotNull(d.Poison));
        Assert.Equal(first.Documents.Select(d => (d.Id, d.Poison)), second.Documents.Select(d => (d.Id, d.Poison)));
    }
}
