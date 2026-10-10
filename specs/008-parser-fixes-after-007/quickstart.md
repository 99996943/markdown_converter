# Quickstart: walidacja spec 008

## Wymagania

- .NET SDK z `global.json`, solucja buduje się (`dotnet build LegalAgent.slnx -c Release`).
- Prywatny korpus właściciela w `tests/LegalAgent.PdfParser.Tests/Corpus/private` (poza git, 19 dokumentów po spec
  007) z kopią goldenów sprzed zmian (`baseline-008/`, krok przygotowania) do porównania zbiorów słów.

## 1. Testy jednostkowe i repliki stron (offline, CI)

```bash
dotnet test tests/LegalAgent.PdfParser.Tests -- --filter-class "*ArtifactRemovalStageTests"
dotnet test tests/LegalAgent.PdfParser.Tests -- --filter-class "*ArtifactCleanupIntegrationTests"
dotnet test tests/LegalAgent.PdfParser.Tests -- --filter-class "*HeadingDetectionStageTests"
dotnet test tests/LegalAgent.PdfParser.Tests -- --filter-class "*CorpusLayoutsIntegrationTests"
dotnet test tests/LegalAgent.PdfParser.Tests -- --filter-class "*GlossaryLayoutTests"
dotnet test tests/LegalAgent.PdfParser.Tests -- --filter-class "*ListLabelPatternsTests"
dotnet test tests/LegalAgent.PdfParser.Tests -- --filter-class "*HangingLabelLayoutTests"
dotnet test tests/LegalAgent.PdfParser.Tests -- --filter-class "*StepSequenceStageTests"
dotnet test tests/LegalAgent.PdfParser.Tests -- --filter-class "*StepSchemesIntegrationTests"
```

Oczekiwane (szczegóły w [contracts/markdown-output.md](./contracts/markdown-output.md)):

- US1: replika ze stopką „n/3” na ≈ 0,905 wysokości strony — bez stopek; „1/2 kwoty”, „12/2024”, „1/” w treści bez
  zmian; jedna strona albo M ≠ liczba stron — nic nie usunięte.
- US2: replika z „Rozdział I (…)” w kolumnie tekstu za etykietą „1.” — element listy, brak nagłówka, rozdziały 17–18 na
  poziomie rozdziału; pogrubione „Art. 42.” po „(uchylony)” zostaje nagłówkiem; termin słowniczka kończący się „/” nie
  jest nagłówkiem.
- US3: zagnieżdżone `- a\.`, `- i\.`; akapit z „np.”, „tj.”, „J. Kowalski”, „A. …”, samotnym „a.” bez listy.
- US4: każda ramka to pogrubiony akapit i treść po nim; punkt poniżej pola obecny; strzałek brak; brak tabeli i
  TBL001.

## 2. Pełna kontrola regresji przed każdym commitem parsera (FR-643 = FR-534 spec 007)

```bash
dotnet test LegalAgent.slnx --filter "Category!=Performance"
LEGALAGENT_PRIVATE_CORPUS="C:/GIT/markdown_converter/tests/LegalAgent.PdfParser.Tests/Corpus/private" \
  LEGALAGENT_CORPUS_REPORT=1 dotnet test tests/LegalAgent.PdfParser.Tests
dotnet run --project src/LegalAgent.Corpus.Cli -c Release -- refresh
dotnet run --project src/LegalAgent.Corpus.Cli -c Release -- verify
LEGALAGENT_CORPUS_FULL=1 dotnet test tests/LegalAgent.Corpus.Tests
```

Oczekiwane: goldeny `Corpus/acts` i `Corpus/banking` bez zmian; `refresh` bez zmian w `corpus/` (poza wersją w
manifeście w kroku zamknięcia); `verify` czysty; `CorpusFull` zielony. Prywatny korpus — zmiany tylko:

| Poprawka | Dokumenty | Zgoda |
|---|---|---|
| US1 | ind-2, firm-1 (wyłącznie usunięte stopki „N/M”) | z góry (FR-644) |
| US2 | corp-1 (D-A) | przegląd wykonawcy (dokument dla firm) |
| US3 | corp-1, corp-3, corp-5 | jw. |
| US4 | firm-2, firm-4, firm-5 | jw.; ind-1, ind-2, firm-3 bez zmian |

Każda inna różnica w dokumencie detalicznym — stop i pytanie do właściciela.

## 3. Porównanie zbiorów słów (FR-645, SC-095)

Pomocnik w testach prywatnego korpusu (raport przy `LEGALAGENT_CORPUS_REPORT=1`) porównuje słowa goldenów z
`baseline-008/` i bieżących. Oczekiwane: utracone słowa — tylko numery „N/M” (ind-2, firm-1) i znaki strzałek; nowe
słowa — tylko odzyskany punkt ramki firm-2 („3. Możemy poprosić o dokument tożsamości…”), wymienione w handoffie.

## 4. Miary sukcesu (SC-090…SC-096)

| Miara | Przed | Po |
|---|---|---|
| Akapity „N/M” (ind-2 / firm-1) | 35 / 6 | 0 / 0 |
| Fałszywe nagłówki jednostek (D-A) | 1 | 0 |
| Etykiety „a.”/„i.” poza listą (corp-1 / corp-3 / corp-5) | 20 / 2 / 9 | 0 / 0 / 0 |
| Ramki jako tabela GFM / wplecione (firm-2) | 7 / 6 | 0 / 0 |
| Utracony tekst ramek (firm-2) | 1 punkt | 0 |
| TBL001 (wszystkie dokumenty) | punkt odniesienia | nie więcej |

## 5. Zamknięcie

```bash
# po podbiciu <Version> w Directory.Build.props
dotnet run --project src/LegalAgent.Corpus.Cli -c Release -- refresh
dotnet run --project src/LegalAgent.Corpus.Cli -c Release -- verify
```

Oczekiwane: `corpus/manifest.json` zapisuje nową wersję, `verify` czysty. Opcjonalnie przebieg FAQ na dokumentach dla
firm (jak spec 007 `quickstart.md` §4) — wymaga klucza właściciela.
