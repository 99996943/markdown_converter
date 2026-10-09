using LegalAgent.Corpus.Composition;
using LegalAgent.Corpus.Content;
using LegalAgent.Corpus.Planning;
using LegalAgent.Corpus.Tests.Unit.Content;
using LegalAgent.Corpus.Validation;

namespace LegalAgent.Corpus.Tests.Unit.Composition;

public sealed class DocumentComposerTests
{
    private const ulong RunSeed = 20261008;

    private const string Templates = """
        id: regulamin-test
        typ: regulaminy
        temat: test
        tytul: ["Regulamin testowy {{param:bank}}"]
        uklady: [jedna-kolumna]
        czolo: { okladka: true, metryczka: false, pola: [oznaczenie, wersja, od, do] }
        parametry:
          produkt: "rachunku testowego"
        sekcje:
          - rodzaj: rozdzial
            tytul: "Postanowienia ogólne"
            wymagane: [test-zakres, test-reklamacje]
          - rodzaj: rozdzial
            tytul: "Postanowienia końcowe"
            wymagane: [test-odwolania]
            opcjonalne: { kategorie: [paragraf-test], min: 0, max: 3 }
          - rodzaj: zalacznik
            tytul: "Wzór wniosku"
            wymagane: [test-zalacznik]
        """;

    private const string ProcedureTemplate = """
        id: procedura-test
        typ: procedury
        temat: test
        tytul: ["Procedura testowa"]
        uklady: [procedura]
        czolo: { okladka: false, metryczka: true, pola: [oznaczenie, wersja, od, wlasciciel, zatwierdzil] }
        parametry:
          wlasciciel: "Departament Operacji"
        sekcje:
          - rodzaj: sekcja
            tytul: "Cel"
            wymagane: [test-cel]
          - rodzaj: sekcja
            tytul: "Opis postępowania"
            wymagane: [test-kroki-a, test-kroki-b]
        """;

    private const string TariffTemplate = """
        id: taryfa-test
        typ: taryfy
        temat: test
        tytul: ["Taryfa testowa"]
        uklady: [taryfa-siatka]
        czolo: { okladka: true, metryczka: false, pola: [oznaczenie, wersja, od] }
        sekcje:
          - rodzaj: sekcja-taryfy
            tytul: "Rachunki"
            wymagane: [test-pozycje-a]
          - rodzaj: sekcja-taryfy
            tytul: "Karty"
            wymagane: [test-pozycje-b]
        """;

    private const string Blocks = """
        bloki:
          - id: test-zakres
            typy: [regulaminy]
            tematy: [test]
            kategoria: paragraf-wymagany
            jednostka: "§ {n}."
            elementy:
              - ustep: "Regulamin określa zasady prowadzenia {{param:produkt}}."
              - ustep: "Ilekroć jest mowa o:"
                punkty:
                  - "Banku – rozumie się {{param:bank}};"
                  - tekst: "Kliencie – rozumie się:"
                    litery: ["konsumenta;", "przedsiębiorcę."]
          - id: test-reklamacje
            typy: [regulaminy]
            tematy: [test]
            kategoria: paragraf-wymagany
            jednostka: "§ {n}."
            elementy:
              - ustep: "Bank rozpatruje reklamację w terminie {{fakt:termin.reklamacja}}.[^1]"
                punkty: ["pisemnie;", "ustnie."]
            przypisy:
              1: "Opłata za duplikat karty wynosi {{fakt:oplata.karta.wydanie-duplikatu}}."
          - id: test-odwolania
            typy: [regulaminy]
            tematy: [test]
            kategoria: paragraf-wymagany
            jednostka: "§ {n}."
            elementy:
              - akapit: "Reklamacje regulują {{ref:blok:test-reklamacje}} oraz {{ref:zalacznik:test-zalacznik}}; opłaty – {{ref:dokument:taryfa-test}}.[^1]"
              - akapit: "Zobacz też {{ref:blok:test-nieobecny}}."
            przypisy:
              1: "Drugi przypis dokumentu."
          - id: test-zalacznik
            typy: [regulaminy]
            tematy: [test]
            kategoria: zalacznik
            elementy:
              - akapit: "Imię i nazwisko wnioskodawcy."
          - id: test-opcja-1
            typy: [regulaminy]
            tematy: [test]
            kategoria: paragraf-test
            jednostka: "§ {n}."
            elementy:
              - akapit: "Postanowienie opcjonalne pierwsze."
          - id: test-opcja-2
            typy: [regulaminy]
            tematy: [test]
            kategoria: paragraf-test
            jednostka: "§ {n}."
            elementy:
              - akapit: "Postanowienie opcjonalne drugie."
          - id: test-cel
            typy: [procedury]
            tematy: [test]
            kategoria: cel
            elementy:
              - akapit: "Procedura określa sposób postępowania."
          - id: test-kroki-a
            typy: [procedury]
            tematy: [test]
            kategoria: krok
            elementy:
              - kroki:
                  - tekst: "Przyjmij wniosek."
                    podkroki:
                      - tekst: "Sprawdź tożsamość."
                      - tekst: "Zarejestruj wniosek."
          - id: test-kroki-b
            typy: [procedury]
            tematy: [test]
            kategoria: krok
            elementy:
              - kroki:
                  - tekst: "Wydaj decyzję."
          - id: test-pozycje-a
            typy: [taryfy]
            tematy: [test]
            kategoria: pozycja-taryfy
            elementy:
              - pozycje-taryfy:
                  - usluga: "Prowadzenie rachunku"
                    stawka: "{{fakt:oplata.karta.wydanie-duplikatu}}"
                    tryb: "miesięcznie"
                  - usluga: "Przelew"
                    stawka: "{{fakt:oplata.prowizja.przelew}}[^1]"
                    tryb: "za przelew"
                    podpozycje:
                      - usluga: "Przelew natychmiastowy"
                        stawka: "5,00 zł"
                        tryb: "za przelew"
            przypisy:
              1: "Nie dotyczy przelewów na rachunki w Banku."
          - id: test-pozycje-b
            typy: [taryfy]
            tematy: [test]
            kategoria: pozycja-taryfy
            elementy:
              - pozycje-taryfy:
                  - usluga: "Wydanie karty"
                    stawka: "0,00 zł"
                    tryb: "jednorazowo"
        """;

    private static readonly Lazy<ContentLibrary> Library = new(() => MiniContent.LoadModified(dir =>
    {
        File.WriteAllText(System.IO.Path.Combine(dir, "szablony", "regulamin-test.yaml"), Templates);
        File.WriteAllText(System.IO.Path.Combine(dir, "szablony", "procedura-test.yaml"), ProcedureTemplate);
        File.WriteAllText(System.IO.Path.Combine(dir, "szablony", "taryfa-test.yaml"), TariffTemplate);
        File.WriteAllText(System.IO.Path.Combine(dir, "bloki", "test.yaml"), Blocks);
    }));

    private static DocumentPlan Plan(string template, string type = "regulaminy", string prefix = "REG", DateOnly? from = null, IReadOnlyList<string>? pool = null, string layout = "jedna-kolumna") => new()
    {
        Id = prefix + "-01",
        Type = type,
        Prefix = prefix,
        Designation = $"BP/{prefix}/01",
        Template = template,
        Layout = layout,
        ValidFrom = from ?? new DateOnly(2026, 5, 1),
        BlockPool = pool ?? ["test-opcja-1", "test-opcja-2"],
        TargetPages = 3,
        Seed = 42,
    };

    private static CompositionResult Regulation(int optional = 10, DateOnly? from = null, IReadOnlyList<FactOverride>? overrides = null) =>
        DocumentComposer.Compose(Plan("regulamin-test", from: from) with { FactOverrides = overrides ?? [] }, Library.Value, RunSeed, optional);

    private static string Text(Element e) => e switch
    {
        HeadingElement h => (h.Label + " " + Inline.PlainText(h.Text)).Trim(),
        ParagraphElement p => Inline.PlainText(p.Text),
        ListItemElement li => li.Label + " " + Inline.PlainText(li.Text),
        TableElement t => string.Join(" | ", t.Rows.Select(r => string.Join(" ; ", r.Select(c => Inline.PlainText(c.Text))))),
        _ => e.GetType().Name,
    };

    private static string All(CompositionResult r) =>
        string.Join("\n", r.Document.Elements.Select(Text)) + "\n" + string.Join("\n", r.Document.Footnotes.Select(f => f.Key + ": " + Inline.PlainText(f.Value)));

    [Fact]
    public void Units_AreNumberedContinuouslyAcrossChapters()
    {
        CompositionResult r = Regulation();

        var units = r.Document.Elements.OfType<HeadingElement>().Where(h => h.Level == 3).Select(h => h.Label).ToList();
        Assert.Equal(["§ 1.", "§ 2.", "§ 3.", "§ 4.", "§ 5."], units);
        var chapters = r.Document.Elements.OfType<HeadingElement>().Where(h => h.Level == 2).Select(Text).ToList();
        Assert.Equal(["Rozdział 1 Postanowienia ogólne", "Rozdział 2 Postanowienia końcowe", "Załącznik nr 1 Wzór wniosku"], chapters);
        Assert.Contains(r.Document.Elements, e => e is PageBreakElement);
        Assert.Equal(2, r.OptionalBlocks);
    }

    [Fact]
    public void Clauses_PointsAndLetters_GetLabelsDepthsAndCitationUnits()
    {
        CompositionResult r = Regulation();
        var items = r.Document.Elements.OfType<ListItemElement>().ToList();

        // § 1 has two clauses → "1." / "2." with points "1)" and letters "a)".
        Assert.Equal(("1.", 0), (items[0].Label, items[0].Depth));
        Assert.Equal(("2.", 0), (items[1].Label, items[1].Depth));
        Assert.Equal(("1)", 1), (items[2].Label, items[2].Depth));
        Assert.Equal(("2)", 1), (items[3].Label, items[3].Depth));
        Assert.Equal(("a)", 2), (items[4].Label, items[4].Depth));
        Assert.Equal(("b)", 2), (items[5].Label, items[5].Depth));
        Assert.Equal("§ 1 ust. 2 pkt 2 lit. b", items[5].Unit);

        // § 2 has a single clause → a paragraph and points at depth 0.
        int unit2 = r.Document.Elements.ToList().FindIndex(e => e is HeadingElement { Label: "§ 2." });
        Assert.IsType<ParagraphElement>(r.Document.Elements[unit2 + 1]);
        var points = r.Document.Elements.Skip(unit2 + 2).Take(2).Cast<ListItemElement>().ToList();
        Assert.Equal([("1)", 0), ("2)", 0)], points.Select(p => (p.Label, p.Depth)));
        Assert.Equal("§ 2 pkt 1", points[0].Unit);
    }

    [Fact]
    public void Facts_TakeTheValueInForceOnValidFrom_AndOverridesWin()
    {
        Assert.Contains("wynosi 30,00 zł.", All(Regulation(from: new DateOnly(2026, 5, 1))), StringComparison.Ordinal);
        Assert.Contains("wynosi 25,00 zł.", All(Regulation(from: new DateOnly(2026, 1, 1))), StringComparison.Ordinal);
        Assert.Contains("terminie 14 dni.", All(Regulation(from: new DateOnly(2026, 5, 1))), StringComparison.Ordinal);
        Assert.Contains("terminie 30 dni.", All(Regulation(from: new DateOnly(2026, 8, 1))), StringComparison.Ordinal);

        CompositionResult overridden = Regulation(overrides: [new FactOverride("termin.reklamacja", new FactValue(Number: 60), OverrideReason.Sprzecznosc)]);
        Assert.Contains("terminie 60 dni.", All(overridden), StringComparison.Ordinal);
        Assert.NotEmpty(overridden.FactElements["termin.reklamacja"]);
    }

    [Fact]
    public void References_AreResolvedAfterNumbering_AndMissingTargetsAreReported()
    {
        CompositionResult r = Regulation();
        string text = All(r);

        Assert.Contains("Reklamacje regulują § 2 oraz Załącznik nr 1; opłaty – Taryfa testowa.", text, StringComparison.Ordinal);
        UnresolvedReference missing = Assert.Single(r.Unresolved);
        Assert.Equal(("REG-01", "test-odwolania", "blok:test-nieobecny"), (missing.DocumentId, missing.BlockId, missing.Target));
    }

    [Fact]
    public void Footnotes_AreNumberedAcrossTheDocument()
    {
        CompositionResult r = Regulation();

        Assert.Equal([1, 2], r.Document.Footnotes.Keys.Order());
        Assert.StartsWith("Opłata za duplikat", Inline.PlainText(r.Document.Footnotes[1]), StringComparison.Ordinal);
        Assert.Equal("Drugi przypis dokumentu.", Inline.PlainText(r.Document.Footnotes[2]));
        Assert.Contains(r.Document.Elements.SelectMany(InlinesOf), i => i.Kind == InlineKind.FootnoteRef && i.Text == "2");
        Assert.DoesNotContain(r.Document.Elements.SelectMany(InlinesOf), i => i.Kind == InlineKind.Reference);
    }

    [Fact]
    public void OptionalBlocks_ComeFromThePoolAtMostOnceAndRespectTheBudget()
    {
        Assert.Equal(0, Regulation(optional: 0).OptionalBlocks);
        Assert.Equal(1, Regulation(optional: 1).OptionalBlocks);
        Assert.Equal(2, DocumentComposer.OptionalCapacity(Plan("regulamin-test"), Library.Value));
        CompositionResult r = Regulation(optional: 10);
        Assert.Equal(1, r.Blocks.Count(b => b.BlockId == "test-opcja-1"));
        Assert.Equal(1, r.Blocks.Count(b => b.BlockId == "test-opcja-2"));
        Assert.Contains(r.Blocks, b => b is { BlockId: "test-zakres", Shared: false } && b.Text.StartsWith("Regulamin określa zasady", StringComparison.Ordinal));
    }

    [Fact]
    public void FrontMatter_HasBankTitleDesignationVersionAndDates()
    {
        CompositionResult r = DocumentComposer.Compose(Plan("regulamin-test") with { Version = 2, ValidTo = new DateOnly(2026, 12, 31) }, Library.Value, RunSeed, 0);

        FrontMatter f = r.Document.Front;
        Assert.Equal(DocumentComposer.BankName, f.Bank);
        Assert.Equal("Regulamin testowy Bank Przykładowy S.A.", f.Title);
        Assert.Equal(f.Title, DocumentComposer.Title(Library.Value.Templates.Single(t => t.Id == "regulamin-test"), Library.Value, RunSeed));
        Assert.Equal("BP/REG/01", f.Designation);
        Assert.Equal(2, f.Version);
        Assert.Equal("1 maja 2026 r.", f.ValidFrom);
        Assert.Equal("31 grudnia 2026 r.", f.ValidTo);
        Assert.True(f.Cover);
        Assert.Equal("jedna-kolumna", r.Document.Layout);
    }

    [Fact]
    public void Procedure_StepsAreNumberedWithinTheirSection_AndTheRecordCardIsFilled()
    {
        CompositionResult r = DocumentComposer.Compose(Plan("procedura-test", "procedury", "PRO", pool: [], layout: "procedura"), Library.Value, RunSeed, 0);

        Assert.Equal(["1. Cel", "2. Opis postępowania"], r.Document.Elements.OfType<HeadingElement>().Select(Text));
        var steps = r.Document.Elements.OfType<ListItemElement>().Select(s => (s.Label, s.Depth, s.Unit)).ToList();
        Assert.Equal([("2.1.", 0, "krok 2.1"), ("2.1.1.", 1, "krok 2.1.1"), ("2.1.2.", 1, "krok 2.1.2"), ("2.2.", 0, "krok 2.2")], steps);
        Assert.True(r.Document.Front.RecordCard);
        Assert.False(r.Document.Front.Cover);
        Assert.Equal("Departament Operacji", r.Document.Front.Owner);
        Assert.NotNull(r.Document.Front.ApprovedBy);
        Assert.Single(r.Document.Front.History);
    }

    [Fact]
    public void Tariff_PositionsBecomeOneTablePerSectionWithNumbersAndNotes()
    {
        CompositionResult r = DocumentComposer.Compose(Plan("taryfa-test", "taryfy", "TAR", pool: [], layout: "taryfa-siatka"), Library.Value, RunSeed, 0);

        Assert.Equal(["I. Rachunki", "II. Karty"], r.Document.Elements.OfType<HeadingElement>().Select(Text));
        var tables = r.Document.Elements.OfType<TableElement>().ToList();
        Assert.Equal(2, tables.Count);
        Assert.Equal(["Lp.", "Wyszczególnienie czynności", "Tryb pobierania", "Stawka"], tables[0].Columns.Select(c => c.Header));
        var rows = tables[0].Rows.Select(row => row.Select(c => Inline.PlainText(c.Text)).ToList()).ToList();
        Assert.Equal(["1.", "Prowadzenie rachunku", "miesięcznie", "30,00 zł"], rows[0]);
        Assert.Equal(["2.", "Przelew", "za przelew", "1,5% 1)"], rows[1]);
        Assert.Equal(["2.1.", "Przelew natychmiastowy", "za przelew", "5,00 zł"], rows[2]);
        Assert.Equal("1) Nie dotyczy przelewów na rachunki w Banku.", Inline.PlainText(Assert.Single(tables[0].Notes)));
        Assert.Equal("3.", Inline.PlainText(tables[1].Rows[0][0].Text));
        Assert.Empty(r.Document.Footnotes);
    }

    [Fact]
    public void SamePlan_ComposesIdentically()
    {
        Assert.Equal(All(Regulation()), All(Regulation()));
    }

    private static IEnumerable<Inline> InlinesOf(Element e) => e switch
    {
        HeadingElement h => h.Text,
        ParagraphElement p => p.Text,
        ListItemElement li => li.Text,
        _ => [],
    };
}
