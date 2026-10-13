# Quickstart: weryfikacja spec 011

Wymagania: SDK z `global.json`, runtime .NET 9; prywatny korpus w
`tests/LegalAgent.PdfParser.Tests/Corpus/private` (ścieżka bezwzględna w zmiennej).

## 1. Testy etapu, replika i bramka

```bash
dotnet test tests/LegalAgent.PdfParser.Tests -- --filter-class "*FootnoteDetectionStageTests"
dotnet test tests/LegalAgent.PdfParser.Tests -- --filter-method "*Footnote*"
dotnet test tests/LegalAgent.PdfParser.Tests -- --filter-class "*GoldenTests"
```

Oczekiwane: nowe testy przechodzą; bramka FTN001: `dz-u-2020-287` 2, `dz-u-2024-1497` 5, `dz-u-2024-1646` 24,
`dz-u-2024-30` 10, `dz-u-2026-1298` 6, `ustawa-o-sluzbie-cywilnej` 11. Na 1.3.0 (przed green) bramka nie przechodzi
dla `ustawa-o-sluzbie-cywilnej` (5) i test etapu nie widzi FTN001 dla definicji bez kreski.

## 2. Pełny zestaw i prywatny korpus

```bash
dotnet test LegalAgent.slnx --filter "Category!=Performance"
LEGALAGENT_PRIVATE_CORPUS="$(pwd)/tests/LegalAgent.PdfParser.Tests/Corpus/private" \
  dotnet test tests/LegalAgent.PdfParser.Tests -- --filter-class "*PrivateCorpusTests"
```

Oczekiwane: 0 zmian goldenów (brak `*.actual.md`), `RegulationNumbers_HaveNoExcess` przechodzi.

## 3. Akt prywatny D20081505Lj

```bash
dotnet run --project src/LegalAgent.PdfParser.Cli -c Release -- convert "Akt prawny - D20081505Lj.pdf" \
  -o out/akt.md --report out/akt.report.json
dotnet run --project src/LegalAgent.PdfParser.Cli -c Release -- chunk "Akt prawny - D20081505Lj.pdf" \
  -o out/akt.chunks.jsonl --id AKT --designation AKT
```

Oczekiwane: `out/akt.md` i `out/akt.chunks.jsonl` identyczne z `publish/porownanie-1.3.0/akt-D20081505Lj.new.*`
(przy tych samych `--id`/`--designation` co przy tworzeniu porównania); raport: 11 × `FTN001_OrphanFootnote`
(s. 13, 67 ×6, 68, 70, 71 ×2).

## 4. Porównanie FTN001 i Markdownu z 1.3.0 (SC-122, SC-125)

Dla wszystkich PDF-ów z `Corpus/acts`, `Corpus/private`, `publish/mBank.FaqGenerator-win-x64/downloads` i katalogu
głównego: liczba FTN001 jak w pomiarze wyjściowym (handoff), poza aktem o służbie cywilnej; Markdown bajt w bajt.

## 5. Korpus syntetyczny i publish/

```bash
dotnet run --project src/LegalAgent.Corpus.Cli -c Release -- refresh
dotnet run --project src/LegalAgent.Corpus.Cli -c Release -- verify
```

Oczekiwane: zmiana tylko wersji parsera w `corpus/manifest.json`. Potem przebudowa `publish/` jak w spec 010 T041
(biblioteki, NuGet 1.3.1, exe generatora FAQ).
