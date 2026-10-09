using LegalAgent.Corpus.Composition;
using LegalAgent.Corpus.Content;

namespace LegalAgent.Corpus.Tests.Unit.Content;

public class ContentLoaderTests
{
    [Fact]
    public void Load_FactsMayBeSplitIntoFilesUnderFakty()
    {
        var lib = MiniContent.LoadModified(dir =>
        {
            Directory.CreateDirectory(System.IO.Path.Combine(dir, "fakty"));
            File.WriteAllText(System.IO.Path.Combine(dir, "fakty", "karty.yaml"), """
                fakty:
                  - id: oplata.karta.dodatkowa
                    rodzaj: kwota
                    wartosci:
                      - wartosc: 12.5
                """);
        });

        Assert.Equal(6, lib.Facts.All.Count);
        Assert.Equal(12.5m, lib.Facts.Get("oplata.karta.dodatkowa").Values[0].Value.Number);
    }

    [Fact]
    public void Load_DuplicateFactAcrossFiles_Throws()
    {
        var ex = Assert.Throws<ContentException>(() => MiniContent.LoadModified(dir =>
        {
            Directory.CreateDirectory(System.IO.Path.Combine(dir, "fakty"));
            File.WriteAllText(System.IO.Path.Combine(dir, "fakty", "dubel.yaml"), """
                fakty:
                  - id: kontakt.infolinia
                    rodzaj: tekst
                    wartosci:
                      - wartosc: "800 000 002"
                """);
        }));

        Assert.Equal("fakty/dubel.yaml", ex.File);
    }

    [Fact]
    public void Load_MiniSet_HasExpectedCounts()
    {
        var lib = MiniContent.Load();

        Assert.Equal(["procedury", "regulaminy", "taryfy"], lib.Types.Select(t => t.Id).Order(StringComparer.Ordinal));
        Assert.Equal(5, lib.Facts.All.Count);
        Assert.Equal(9, lib.Templates.Count);
        Assert.Equal(28, lib.Blocks.Count);
        Assert.Single(lib.PoisonKinds);
        Assert.Equal("POL", lib.PoisonKinds[0].Abbreviation);
        Assert.Single(lib.PoisonPatterns);
        Assert.Equal(["Bank Fikcyjny Zakazany"], lib.ForbiddenNames);
        Assert.Single(lib.Acts);
        Assert.Equal(new DateOnly(2025, 5, 9), lib.Acts[0].ConsolidatedDate);
        Assert.Equal("dz-u-2025-644-aml.pdf", lib.Acts[0].File);
    }

    [Fact]
    public void Load_MiniSet_ReadsClauseWithPointsAndLetters()
    {
        var block = MiniContent.Load().Blocks.Single(b => b.Id == "karty-zastrzezenie");

        Assert.Equal("§ {n}.", block.Unit);
        Assert.Equal(["regulaminy"], block.Types);
        Assert.Equal(["karty"], block.Topics);
        var clause = Assert.IsType<SourceClause>(Assert.Single(block.Elements));
        Assert.Equal(2, clause.Points.Count);
        Assert.Empty(clause.Points[0].Letters);
        Assert.Equal(["numer karty;", "datę zdarzenia.[^1]"], clause.Points[1].Letters);
        Assert.Equal("Opłata za duplikat: {{fakt:oplata.karta.wydanie-duplikatu}}.", block.Footnotes["1"]);
        Assert.Equal("bloki/regulaminy/karty.yaml", block.File);
    }

    [Fact]
    public void Load_MiniSet_ReadsTariffItemWithChildren()
    {
        var block = MiniContent.Load().Blocks.Single(b => b.Id == "taryfa-karty-oplaty");

        var items = Assert.IsType<SourceTariffItems>(Assert.Single(block.Elements)).Items;
        Assert.Equal(3, items.Count);
        Assert.Equal("Przelew", items[1].Service);
        var child = Assert.Single(items[1].Children);
        Assert.Equal("Przelew zagraniczny", child.Service);
        Assert.Equal("za przelew", child.Mode);
    }

    [Fact]
    public void Load_MiniSet_ReadsChecklistFormTableStepsAndScheme()
    {
        var lib = MiniContent.Load();

        var checklist = Assert.IsType<SourceChecklist>(Assert.Single(lib.Blocks.Single(b => b.Id == "procedura-lista").Elements));
        Assert.Equal(ChecklistForm.Table, checklist.Form);
        Assert.Equal(2, checklist.Items.Count);

        var kroki = lib.Blocks.Single(b => b.Id == "procedura-kroki").Elements.OfType<SourceSteps>().Single();
        Assert.Equal(2, kroki.Steps.Count);
        Assert.Equal("Sprawdź dokument tożsamości.", kroki.Steps[0].Children[0].Children[0].Text);

        var scheme = Assert.IsType<SourceScheme>(Assert.Single(lib.Blocks.Single(b => b.Id == "procedura-schemat").Elements));
        Assert.Equal(["Pracownik rejestruje zgłoszenie."], scheme.Steps[0].Explanation);
        Assert.Equal(2, scheme.Steps[1].Explanation.Count);
    }

    [Fact]
    public void Load_MiniSet_ReadsTableHeadingCalloutAndRecordCard()
    {
        var lib = MiniContent.Load();

        var table = lib.Blocks.Single(b => b.Id == "karty-tabela").Elements.OfType<SourceTable>().Single();
        Assert.Equal(2.0, table.Columns[0].Weight);
        Assert.Equal(1.0, table.Columns[1].Weight);
        Assert.Equal(2, table.Rows.Count);
        Assert.True(table.Grid);
        Assert.Equal(["Limity dzienne."], table.Notes);

        var shared = lib.Blocks.Single(b => b.Id == "wspolne-definicje");
        Assert.True(shared.Shared);
        Assert.Equal("Definicje", shared.Title);
        Assert.Equal(2, shared.Elements.OfType<SourceHeading>().Single().Level);
        Assert.Single(shared.Elements.OfType<SourceCallout>());

        var card = lib.Blocks.Single(b => b.Id == "procedura-kroki").Elements.OfType<SourceRecordCard>().Single();
        Assert.Equal("Właściciel", card.Pairs[0].Key);
    }

    [Fact]
    public void Load_MiniSet_ReadsTemplatesAndPoisonPatterns()
    {
        var lib = MiniContent.Load();

        var template = lib.Templates.Single(t => t.Id == "regulamin-karty");
        Assert.Equal("regulaminy", template.Type);
        Assert.True(template.Front.Cover);
        Assert.Equal(["oznaczenie", "wersja", "od", "do"], template.Front.Fields);
        var slot = Assert.Single(template.Sections);
        Assert.Equal(new OptionalSpec(["paragraf"], 1, 2), slot.Optional, new OptionalSpecComparer());
        Assert.Equal("karty debetowej", template.Parameters["produkt"]);

        var pattern = Assert.Single(lib.PoisonPatterns);
        Assert.Equal("polecenia-dla-ai", pattern.Kind);
        Assert.Equal("zmiana-odpowiedzi", pattern.Goal);
        Assert.Equal(["przypis", "komorka-tabeli", "akapit"], pattern.Placements);
    }

    [Fact]
    public void Load_TypeWithoutMinLayouts_HasEmptyMap()
        => Assert.All(MiniContent.Load().Types, t => Assert.Empty(t.MinLayouts));

    [Fact]
    public void Load_MinLayouts_AreRead()
    {
        var lib = MiniContent.LoadModified(dir => MiniContent.Replace(
            dir, "typy.yaml", "    nazwa: regulamin\n", "    nazwa: regulamin\n    uklady-min: { dwie-kolumny: 2, tabela-dokument: 1 }\n"));

        var map = lib.Types.Single(t => t.Id == "regulaminy").MinLayouts;
        Assert.Equal(2, map["dwie-kolumny"]);
        Assert.Equal(1, map["tabela-dokument"]);
    }

    [Fact]
    public void Load_MinLayoutsUnknownLayout_NamesPath()
    {
        var ex = Assert.Throws<ContentException>(() => MiniContent.LoadModified(dir => MiniContent.Replace(
            dir, "typy.yaml", "    nazwa: regulamin\n", "    nazwa: regulamin\n    uklady-min: { trzy-kolumny: 2 }\n")));

        Assert.Equal("typy.yaml", ex.File);
        Assert.Contains("trzy-kolumny", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_MinLayoutsBelowOne_Throws()
    {
        var ex = Assert.Throws<ContentException>(() => MiniContent.LoadModified(dir => MiniContent.Replace(
            dir, "typy.yaml", "    nazwa: regulamin\n", "    nazwa: regulamin\n    uklady-min: { dwie-kolumny: 0 }\n")));

        Assert.Equal("typy.yaml", ex.File);
    }

    [Fact]
    public void Load_UnknownBlockKey_NamesFileAndPath()
    {
        var ex = Assert.Throws<ContentException>(() => MiniContent.LoadModified(dir =>
            MiniContent.Replace(dir, "bloki/regulaminy/karty.yaml", "    kategoria: paragraf\n    jednostka", "    kategoria: paragraf\n    literowka: x\n    jednostka")));

        Assert.Equal("bloki/regulaminy/karty.yaml", ex.File);
        Assert.Equal("bloki[0].literowka", ex.YamlPath);
        Assert.Contains("bloki/regulaminy/karty.yaml", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_UnknownElementModifier_NamesPath()
    {
        var ex = Assert.Throws<ContentException>(() => MiniContent.LoadModified(dir =>
            MiniContent.Replace(dir, "bloki/wspolne/definicje.yaml", "      - akapit: \"Użyte", "      - akapit: \"Użyte\"\n        poziom: 2\n      - akapit: \"Użyte")));

        Assert.Equal("bloki/wspolne/definicje.yaml", ex.File);
        Assert.Equal("bloki[0].elementy[1].poziom", ex.YamlPath);
    }

    [Fact]
    public void Load_DuplicateBlockId_NamesBothFiles()
    {
        var ex = Assert.Throws<ContentException>(() => MiniContent.LoadModified(dir =>
            MiniContent.Replace(dir, "bloki/taryfy/karty.yaml", "id: taryfa-karty-oplaty", "id: karty-tabela")));

        Assert.Contains("karty-tabela", ex.Message, StringComparison.Ordinal);
        Assert.Contains("bloki/regulaminy/karty.yaml", ex.Message, StringComparison.Ordinal);
        Assert.Contains("bloki/taryfy/karty.yaml", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_BlockWithoutTypy_ThrowsWithPath()
    {
        var ex = Assert.Throws<ContentException>(() => MiniContent.LoadModified(dir =>
            MiniContent.Replace(dir, "bloki/taryfy/karty.yaml", "    typy: [taryfy]\n", string.Empty)));

        Assert.Equal("bloki/taryfy/karty.yaml", ex.File);
        Assert.Equal("bloki[0].typy", ex.YamlPath);
    }

    [Fact]
    public void Load_PoisonCommandWithoutGoal_Throws()
    {
        var ex = Assert.Throws<ContentException>(() => MiniContent.LoadModified(dir =>
            MiniContent.Replace(dir, "zatrucia/polecenia-dla-ai.yaml", "    cel: zmiana-odpowiedzi\n", string.Empty)));

        Assert.Equal("zatrucia/polecenia-dla-ai.yaml", ex.File);
        Assert.Equal("wzorce[0].cel", ex.YamlPath);
    }

    [Fact]
    public void Load_TemplateLayoutOutsideList_Throws()
    {
        var ex = Assert.Throws<ContentException>(() => MiniContent.LoadModified(dir =>
            MiniContent.Replace(dir, "szablony/procedura-reklamacji.yaml", "uklady: [procedura]", "uklady: [trzy-kolumny]")));

        Assert.Equal("szablony/procedura-reklamacji.yaml", ex.File);
        Assert.Equal("uklady[0]", ex.YamlPath);
    }

    [Fact]
    public void Load_TemplateTypeNotInTypy_Throws()
    {
        var ex = Assert.Throws<ContentException>(() => MiniContent.LoadModified(dir =>
            MiniContent.Replace(dir, "szablony/taryfa-karty.yaml", "typ: taryfy", "typ: nieznane")));

        Assert.Equal("szablony/taryfa-karty.yaml", ex.File);
        Assert.Equal("typ", ex.YamlPath);
        Assert.Contains("nieznane", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_DuplicateTypeId_Throws()
    {
        var ex = Assert.Throws<ContentException>(() => MiniContent.LoadModified(dir =>
            MiniContent.Replace(dir, "typy.yaml", "id: taryfy", "id: regulaminy")));

        Assert.Equal("typy.yaml", ex.File);
        Assert.Contains("regulaminy", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_DuplicateTypePrefix_Throws()
    {
        var ex = Assert.Throws<ContentException>(() => MiniContent.LoadModified(dir =>
            MiniContent.Replace(dir, "typy.yaml", "prefiks: TAR", "prefiks: REG")));

        Assert.Equal("typy.yaml", ex.File);
        Assert.Contains("REG", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_UnknownFactInBlockText_NamesFileAndPath()
    {
        var ex = Assert.Throws<ContentException>(() => MiniContent.LoadModified(dir =>
            MiniContent.Replace(dir, "bloki/wspolne/definicje.yaml", "{{fakt:kontakt.infolinia}}", "{{fakt:nie.istnieje}}")));

        Assert.Equal("bloki/wspolne/definicje.yaml", ex.File);
        Assert.Equal("bloki[0].elementy[2].ramka", ex.YamlPath);
        Assert.Contains("nie.istnieje", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_RequiredBlockMissing_Throws()
    {
        var ex = Assert.Throws<ContentException>(() => MiniContent.LoadModified(dir =>
            MiniContent.Replace(dir, "szablony/procedura-reklamacji.yaml", "procedura-lista", "brak-bloku")));

        Assert.Equal("szablony/procedura-reklamacji.yaml", ex.File);
        Assert.Contains("brak-bloku", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_BlocksFollowFileOrderThenInFileOrder()
    {
        var ids = MiniContent.Load().Blocks.Select(b => b.Id).ToList();

        // Files in ordinal path order (procedury, regulaminy, taryfy, wspolne), blocks in file order within a file.
        Assert.Equal(
            [
                "procedura-kroki", "procedura-schemat", "procedura-lista",
                "karty-zastrzezenie", "karty-tabela",
                "taryfa-karty-oplaty",
                "wspolne-definicje",
            ],
            ids.Where(i => i is "procedura-kroki" or "procedura-schemat" or "procedura-lista" or "karty-zastrzezenie" or "karty-tabela" or "taryfa-karty-oplaty" or "wspolne-definicje"));
        Assert.True(ids.IndexOf("procedura-lista") < ids.IndexOf("karty-zastrzezenie"));
        Assert.True(ids.IndexOf("karty-tabela") < ids.IndexOf("taryfa-karty-oplaty"));
        Assert.Equal("wspolne-definicje", ids[^1]);
    }

    [Fact]
    public void Load_ContentHash_StableAndSensitiveToOneByte()
    {
        var first = MiniContent.Load().ContentHash;
        var second = MiniContent.Load().ContentHash;
        var changed = MiniContent.LoadModified(dir =>
            MiniContent.Replace(dir, "zabronione.yaml", "Zakazany", "Zakazanx")).ContentHash;

        Assert.Equal(first, second);
        Assert.Matches("^[0-9a-f]{64}$", first);
        Assert.NotEqual(first, changed);
    }

    private sealed class OptionalSpecComparer : IEqualityComparer<OptionalSpec?>
    {
        public bool Equals(OptionalSpec? x, OptionalSpec? y) =>
            x is not null && y is not null && x.Min == y.Min && x.Max == y.Max && x.Categories.SequenceEqual(y.Categories);

        public int GetHashCode(OptionalSpec? obj) => obj?.Min ?? 0;
    }
}
