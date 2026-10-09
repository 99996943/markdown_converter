using System.Globalization;
using LegalAgent.Corpus.Content;
using LegalAgent.Corpus.Planning;
using LegalAgent.Corpus.Tests.Unit.Content;

namespace LegalAgent.Corpus.Tests.Unit.Planning;

public class CorpusPlannerTests
{
    private static readonly ContentLibrary Mini = MiniContent.Load();

    private static RunParameters Parameters(int perType = 6, ulong seed = 42, IReadOnlyList<string>? types = null)
        => new() { Seed = seed, DocumentsPerType = perType, Types = types, Pages = new PageRange(5, 9) };

    private static string Serialise(CorpusPlan plan) => string.Join(
        "\n",
        plan.Documents.Select(d => string.Join(
            "|",
            d.Id,
            d.Type,
            d.Prefix,
            d.Designation,
            d.Template,
            d.Layout,
            d.Version.ToString(CultureInfo.InvariantCulture),
            d.ValidFrom.ToString("O", CultureInfo.InvariantCulture),
            d.ValidTo?.ToString("O", CultureInfo.InvariantCulture),
            d.Status,
            d.PreviousVersionId,
            string.Join(",", d.BlockPool),
            d.TargetPages.ToString(CultureInfo.InvariantCulture),
            d.Seed.ToString(CultureInfo.InvariantCulture),
            d.FactOverrides.Count.ToString(CultureInfo.InvariantCulture))));

    [Fact]
    public void Plan_ProducesDocumentsPerTypeWithIdsAndDesignations()
    {
        var plan = CorpusPlanner.Plan(Mini, Parameters());

        Assert.Equal(18, plan.Documents.Count);
        Assert.Equal(["REG-01", "REG-02", "REG-03", "REG-04", "REG-05", "REG-06"], plan.Documents.Where(d => d.Type == "regulaminy").Select(d => d.Id));
        Assert.Equal(["regulaminy", "taryfy", "procedury"], plan.Documents.Select(d => d.Type).Distinct());
        Assert.Equal(plan.Documents.Count, plan.Documents.Select(d => d.Designation).Distinct(StringComparer.Ordinal).Count());
        var third = plan.Documents.Single(d => d.Id == "REG-03");
        Assert.Equal("BP/REG/03", third.Designation);
        Assert.Equal("REG", third.Prefix);
        Assert.Equal("PRO-06", plan.Documents[^1].Id);
        Assert.All(plan.Documents, d =>
        {
            Assert.Equal(1, d.Version);
            Assert.Null(d.ValidTo);
            Assert.Equal(DocumentStatus.Obowiazujacy, d.Status);
            Assert.Empty(d.FactOverrides);
        });
    }

    [Fact]
    public void Plan_HundredDocuments_UseThreeDigitIds()
    {
        var plan = CorpusPlanner.Plan(Mini, Parameters(perType: 100, types: ["taryfy"]));

        Assert.Equal("TAR-001", plan.Documents[0].Id);
        Assert.Equal("TAR-100", plan.Documents[^1].Id);
        Assert.Equal("BP/TAR/007", plan.Documents[6].Designation);
    }

    [Fact]
    public void Plan_AssignsTemplatesRoundRobin()
    {
        var plan = CorpusPlanner.Plan(Mini, Parameters(perType: 7));

        foreach (var group in plan.Documents.GroupBy(d => d.Type))
        {
            var docs = group.ToList();
            var counts = docs.GroupBy(d => d.Template).Select(g => g.Count()).ToList();
            Assert.Equal(3, counts.Count);
            Assert.All(counts, c => Assert.InRange(c, 2, 3));
            Assert.Equal(docs[0].Template, docs[3].Template);
            Assert.Equal(docs[1].Template, docs[4].Template);
            Assert.Equal(docs[0].Template, docs[6].Template);
            Assert.All(docs, d => Assert.Equal(group.Key, Mini.Templates.Single(t => t.Id == d.Template).Type));
        }
    }

    [Fact]
    public void Plan_LayoutIsAllowedByTemplate()
    {
        var plan = CorpusPlanner.Plan(Mini, Parameters(perType: 10));

        Assert.All(plan.Documents, d => Assert.Contains(d.Layout, Mini.Templates.Single(t => t.Id == d.Template).Layouts));
    }

    [Fact]
    public void Plan_MinLayouts_AreSatisfied()
    {
        var content = MiniContent.LoadModified(dir => MiniContent.Replace(
            dir, "typy.yaml", "    nazwa: regulamin\n", "    nazwa: regulamin\n    uklady-min: { dwie-kolumny: 2, tabela-dokument: 2 }\n"));

        for (ulong seed = 1; seed <= 5; seed++)
        {
            var plan = CorpusPlanner.Plan(content, Parameters(perType: 6, seed: seed, types: ["regulaminy"]));

            Assert.True(plan.Documents.Count(d => d.Layout == "dwie-kolumny") >= 2);
            Assert.True(plan.Documents.Count(d => d.Layout == "tabela-dokument") >= 2);
        }
    }

    [Fact]
    public void Plan_MinLayoutsImpossible_NamesTypeAndLayout()
    {
        var content = MiniContent.LoadModified(dir => MiniContent.Replace(
            dir, "typy.yaml", "    nazwa: regulamin\n", "    nazwa: regulamin\n    uklady-min: { taryfa-siatka: 1 }\n"));

        var ex = Assert.Throws<CorpusGenerationException>(() => CorpusPlanner.Plan(content, Parameters()));

        Assert.Contains("regulaminy", ex.Message, StringComparison.Ordinal);
        Assert.Contains("taryfa-siatka", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Plan_MinLayoutsMoreThanDocuments_Throws()
    {
        var content = MiniContent.LoadModified(dir => MiniContent.Replace(
            dir, "typy.yaml", "    nazwa: regulamin\n", "    nazwa: regulamin\n    uklady-min: { dwie-kolumny: 7 }\n"));

        Assert.Throws<CorpusGenerationException>(() => CorpusPlanner.Plan(content, Parameters(perType: 6)));
    }

    [Fact]
    public void Plan_BlockPools_AreDisjointAndAllowed()
    {
        var plan = CorpusPlanner.Plan(Mini, Parameters(perType: 6));

        var all = plan.Documents.SelectMany(d => d.BlockPool).ToList();
        Assert.Equal(all.Count, all.Distinct(StringComparer.Ordinal).Count());
        Assert.NotEmpty(all);
        var required = Mini.Templates.SelectMany(t => t.Sections).SelectMany(s => s.Required).ToHashSet(StringComparer.Ordinal);
        foreach (var doc in plan.Documents)
        {
            var template = Mini.Templates.Single(t => t.Id == doc.Template);
            var categories = template.Sections.Where(s => s.Optional is not null).SelectMany(s => s.Optional!.Categories).ToHashSet(StringComparer.Ordinal);
            Assert.Equal(doc.BlockPool.Order(StringComparer.Ordinal), doc.BlockPool);
            foreach (var id in doc.BlockPool)
            {
                var block = Mini.Blocks.Single(b => b.Id == id);
                Assert.False(block.Shared);
                Assert.Contains(doc.Type, block.Types);
                Assert.Contains(block.Category, categories);
                Assert.True(block.Topics.Count == 0 || block.Topics.Contains(template.Topic));
                Assert.DoesNotContain(id, required);
            }
        }
    }

    [Fact]
    public void Plan_EveryDocumentGetsSomeBlocksWhenEnoughExist()
    {
        var plan = CorpusPlanner.Plan(Mini, Parameters(perType: 3));

        Assert.All(plan.Documents, d => Assert.NotEmpty(d.BlockPool));
    }

    [Fact]
    public void Plan_TargetPages_WithinRange()
    {
        var plan = CorpusPlanner.Plan(Mini, Parameters(perType: 20));

        Assert.All(plan.Documents, d => Assert.InRange(d.TargetPages, 5, 9));
        Assert.True(plan.Documents.Select(d => d.TargetPages).Distinct().Count() > 1);
    }

    [Fact]
    public void Plan_ValidFrom_IsFirstOfMonthBeforeReferenceDate()
    {
        var parameters = Parameters(perType: 20);
        var plan = CorpusPlanner.Plan(Mini, parameters);

        Assert.All(plan.Documents, d =>
        {
            Assert.Equal(1, d.ValidFrom.Day);
            Assert.True(d.ValidFrom <= parameters.ReferenceDate.AddMonths(-1));
            Assert.True(d.ValidFrom >= parameters.ReferenceDate.AddMonths(-19).AddDays(-31));
        });
    }

    [Fact]
    public void Plan_SeedOfDocument_IsDerivedFromRunSeedAndId()
    {
        var plan = CorpusPlanner.Plan(Mini, Parameters(seed: 7));

        Assert.All(plan.Documents, d => Assert.Equal(LegalAgent.Corpus.Random.DeterministicRandom.DeriveSeed(7, "dokument", d.Id), d.Seed));
    }

    [Fact]
    public void Plan_SameInputs_SamePlan()
        => Assert.Equal(Serialise(CorpusPlanner.Plan(Mini, Parameters())), Serialise(CorpusPlanner.Plan(MiniContent.Load(), Parameters())));

    [Fact]
    public void Plan_OtherSeed_DifferentPlan()
        => Assert.NotEqual(Serialise(CorpusPlanner.Plan(Mini, Parameters(seed: 1))), Serialise(CorpusPlanner.Plan(Mini, Parameters(seed: 2))));

    [Fact]
    public void Plan_DocumentDoesNotDependOnOtherRequestedTypes()
    {
        var alone = CorpusPlanner.Plan(Mini, Parameters(types: ["regulaminy"])).Documents.Single(d => d.Id == "REG-03");
        var all = CorpusPlanner.Plan(Mini, Parameters()).Documents.Single(d => d.Id == "REG-03");

        Assert.Equal(Serialise(new CorpusPlan([alone])), Serialise(new CorpusPlan([all])));
    }

    [Fact]
    public void Plan_UnknownType_Throws()
    {
        var ex = Assert.Throws<CorpusGenerationException>(() => CorpusPlanner.Plan(Mini, Parameters(types: ["nieznane"])));

        Assert.Contains("nieznane", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Plan_TypeWithoutTemplates_Throws()
    {
        var content = Mini with { Templates = [.. Mini.Templates.Where(t => t.Type != "taryfy")] };

        var ex = Assert.Throws<CorpusGenerationException>(() => CorpusPlanner.Plan(content, Parameters()));

        Assert.Contains("taryfy", ex.Message, StringComparison.Ordinal);
    }
}
