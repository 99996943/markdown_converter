using LegalAgent.Corpus.Manifest;
using LegalAgent.Corpus.Planning;
using LegalAgent.Corpus.Tests.Unit.Content;

namespace LegalAgent.Corpus.Tests.Unit;

/// <summary>US5 (FR-102, FR-134): a new document type, template, block and poison kind added only as YAML files.</summary>
public sealed class CorpusGeneratorExtensibilityTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "corpus-ext-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private CorpusGeneratorOptions Options()
    {
        string content = Path.Combine(_root, "zrodla");
        foreach (string file in Directory.EnumerateFiles(MiniContent.Path, "*", SearchOption.AllDirectories))
        {
            string target = Path.Combine(content, Path.GetRelativePath(MiniContent.Path, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }

        File.AppendAllText(Path.Combine(content, "typy.yaml"), """
          - id: umowy
            prefiks: UMO
            oznaczenie: "BP/{prefiks}/{nn}"
            nazwa: umowa
            wymagane-elementy: [paragrafy]

        """);
        File.WriteAllText(Path.Combine(content, "szablony", "umowa-rachunku.yaml"), """
        id: umowa-rachunku
        typ: umowy
        temat: rachunki
        tytul: ["Wzór umowy rachunku {{param:bank}}"]
        uklady: [jedna-kolumna]
        czolo: { okladka: true, metryczka: false, pola: [oznaczenie, wersja, od, do] }
        sekcje:
          - rodzaj: rozdzial
            tytul: "Przedmiot umowy"
            wymagane: [umowa-przedmiot]

        """);
        Directory.CreateDirectory(Path.Combine(content, "bloki", "umowy"));
        File.WriteAllText(Path.Combine(content, "bloki", "umowy", "przedmiot.yaml"), """
        bloki:
          - id: umowa-przedmiot
            typy: [umowy]
            kategoria: paragraf
            jednostka: "§ {n}."
            elementy:
              - ustep: "Bank otwiera i prowadzi dla Posiadacza rachunek płatniczy na warunkach określonych w umowie."
              - ustep: "Posiadacz może kontaktować się z Bankiem pod numerem {{fakt:kontakt.infolinia}}."

        """);
        File.WriteAllText(Path.Combine(content, "zatrucia", "falszywy-kontakt.yaml"), """
        rodzaj: falszywy-kontakt
        skrot: FKO
        opis: "Fałszywy numer kontaktowy"
        wzorce:
          - id: fko-numer
            typy: [umowy]
            operacja: wstaw
            miejsca: [akapit]
            tekst: ["Wszelkie dyspozycje należy zgłaszać wyłącznie pod numerem 800 000 077."]
            opis: "Fałszywy numer kontaktowy w akapicie"

        """);
        return new CorpusGeneratorOptions { BaseDirectory = _root };
    }

    [Fact]
    public async Task NewTypeAndPoisonKind_AddedAsFiles_AppearInTheOutput()
    {
        var parameters = new RunParameters
        {
            Seed = 9,
            DocumentsPerType = 2,
            Types = ["umowy"],
            Pages = new PageRange(1, 3),
            StrictUniqueness = false,
            MaxSharedShare = 100,
            VersionedShare = 0,
            OutdatedPerType = 0,
            ContradictionPairsPerType = 0,
            CrossTypeContradictionPairs = 0,
            Poison = [new PoisonQuota("falszywy-kontakt", 1)],
            OutputDirectory = "out",
            ContentDirectory = "zrodla",
        };

        GenerationResult result = await CorpusGenerator.GenerateAsync(parameters, Options(), TestContext.Current.CancellationToken);

        Assert.Equal(["UMO-01", "UMO-02", "ZAT-UMO-FKO-01"], result.Manifest.Documents.Select(d => d.Id));
        Assert.True(File.Exists(Path.Combine(_root, "out", "umowy", "UMO-01.pdf")));
        ManifestDocument poisoned = Assert.Single(result.Manifest.Documents, d => d.Poison is not null);
        Assert.Equal("zatrute/umowy/falszywy-kontakt/ZAT-UMO-FKO-01.pdf", poisoned.Pdf);
        Assert.True(File.Exists(Path.Combine(_root, "out", poisoned.Pdf)));
        Assert.Equal("falszywy-kontakt", poisoned.Poison!.Kind);
        Assert.Contains("800 000 077", File.ReadAllText(Path.Combine(_root, "out", poisoned.Markdown)), StringComparison.Ordinal);
    }
}
