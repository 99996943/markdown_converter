# Quickstart: walidacja spec 010

## Wymagania

- SDK z `global.json`, .NET 9; gałąź `010-bank-table-footnotes`.
- Prywatny korpus: `LEGALAGENT_PRIVATE_CORPUS=C:/GIT/markdown_converter/tests/LegalAgent.PdfParser.Tests/Corpus/private`
  (ścieżka bezwzględna), z `number-check.txt` i kopią goldenów sprzed zmian w `baseline-010/`.
- Regulaminy bez goldenów: `publish/mBank.FaqGenerator-win-x64/downloads/*.pdf` (downloads-1…5, mapowanie w
  `publish/docling-eval/input/mapping.tsv`), wzorzec kształtu: `publish/docling-eval/docling/<id>.md`.

## 1. Testy replik (offline, bez korpusu prywatnego)

```bash
dotnet test tests/LegalAgent.PdfParser.Tests -- --filter-class "*BankTablesIntegrationTests"      # repliki T1–T12
dotnet test tests/LegalAgent.PdfParser.Tests -- --filter-class "*RuledTableGridTests"
dotnet test tests/LegalAgent.PdfParser.Tests -- --filter-class "*FootnoteDetectionStageTests"
dotnet test tests/LegalAgent.PdfParser.Tests -- --filter-class "*NumberCheckTests"
dotnet test tests/LegalAgent.Chunking.Tests -- --filter-class "*SplitTablesTests"            # część od środka grupy
```

Oczekiwane: każda replika ma parę commitów red/green (FR-752); wyniki jak w
[contracts/markdown-output.md](./contracts/markdown-output.md).

## 2. Pełny zestaw kontroli przed commitem parsera (FR-753)

```bash
dotnet build LegalAgent.slnx -c Release
LEGALAGENT_PRIVATE_CORPUS=<abs> LEGALAGENT_CORPUS_FULL=1 LEGALAGENT_CORPUS_REPORT=1 \
  dotnet test LegalAgent.slnx --filter "Category!=Performance"
dotnet run --project src/LegalAgent.Corpus.Cli -c Release -- refresh
dotnet run --project src/LegalAgent.Corpus.Cli -c Release -- verify
```

Oczekiwane: 0 niepowodzeń; `RegulationNumbers_HaveNoExcess` zielony; kolumna „Odnośniki sklejone” maleje w
dokumentach z przypadków T12; `verify` czysty; zmiany goldenów tylko według FR-755 (prywatne corp-*/firm-*/ind-* w
tabelach i przypisach T1–T12) — inne: stop i pytanie.

## 3. Porównanie słów przed/po (SC-115)

Dla każdego prywatnego dokumentu: wielozbiór słów `baseline-010/<doc>.expected.md` vs nowy golden, po usunięciu
znaczników `[^n]`, `<!-- page -->`, ucieczek i składni tabel; od nowego wielozbioru odejmowane są słowa z
powtórzeń (komórki `ContinuesAbove`, tekst `HeaderGroup` w kolumnach 2…k) według modelu z konwersji. Oczekiwane:
0 utraconych słów, 0 słów spoza PDF; zmienione tokeny tylko w tabelach i przypisach z przypadków.

## 4. Przegląd przypadków na prawdziwych dokumentach (SC-110…SC-114)

```bash
dotnet run --project src/LegalAgent.PdfParser.Cli -c Release -- convert <pdf> -o <scratch>/<id>.md --report <scratch>/<id>.report.json
```

| Przypadek | Dokument, strony | Sprawdzić |
|---|---|---|
| T11 | firm-5 s. 13 | 10 wierszy, w każdym etykieta grupy |
| T12 | firm-5 s. 13, firm-1 s. 16–17, downloads-3 s. 17 | `[^n]` zamiast „3kurs”, „Tak4”, „TAK/TAK5”; definicje `[^n]:` |
| T3 | downloads-2 s. 20–21, downloads-3 s. 18, downloads-4 s. 16–17, downloads-5 s. 6–7, firm-3 s. 8, ind-4 s. 12 | liczba kolumn jak w Doclingu, 0 pustych kolumn, wartości pod właściwymi nagłówkami |
| T1/T2 | ind-4 s. 42–46, ind-2 s. 33–36, downloads-5 s. 26–27, downloads-3 s. 17, firm-5 s. 22–23, firm-1 s. 16–17 | jedna tabela GFM, 0 TBL001, liczba wierszy jak w PDF |
| T4–T6 | firm-5 s. 12–13, ind-2 s. 6–7, ind-4 s. 12–13, downloads-5 s. 7–8 | tabele kompletne, 0 fałszywych `##`, komórka „…, czyli:” w całości |

Wynik przeglądu (tabela przed/po) trafia do handoffu w `plan.md`. Prywatne treści nie trafiają do repozytorium.

## 5. Wydanie

`Directory.Build.props` 1.3.0; `refresh` + `verify` (`parserVersion`, `generatorVersion` w `corpus/manifest.json`);
README parsera („Znane ograniczenia”: T7, T8, T10, „BLIK⁹,¹⁰”, warianty bez linii, jeśli nie zrobione), CLAUDE.md
(spec 010 w opisie architektury), handoff; przebudowa `publish/` i uruchomienie generatora FAQ na regulaminach z
publish/ bez błędów konwersji (SC-117).
