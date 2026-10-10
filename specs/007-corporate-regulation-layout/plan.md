# Implementation Plan: Regulaminy z etykietami w wysuniętej kolumnie i paragrafami „§ N”

**Branch**: `007-corporate-regulation-layout` (od `main` po scaleniu PR #8) | **Date**: 2026-10-10 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/007-corporate-regulation-layout/spec.md`

## Summary

Cztery regulaminy mBanku dla firm (D-A…D-D) mają etykiety „1.”, „1/”, „a/” w wysuniętej kolumnie, paragrafy „§ N”
jako wyśrodkowany pogrubiony wiersz i słowniczek z terminem po lewej. Parser zamienia to w tabele zastępcze (TBL001:
132/16/58/79), dzieli zawinięte wiersze i gubi jednostki „§”.

Badania (research R1–R3, pomiar geometrii na prawdziwych stronach) pokazały, że układ etykiet jest **taki sam jak w
aktach ISAP** — różni się tylko składnia „1/”, „a/”, której parser nie zna. Podejście:

1. **US2 — etykiety z ukośnikiem**: nowe rodzaje `ArabicSlash`, `LetterSlash` w `ListLabelPatterns` i dopisanie ich
   tam, gdzie TableDetection i ListDetection obsługują „1)”, „a)”; uogólnienie `IsHangingList`. Eksperyment na kopii:
   TBL001 132→6, 16→0, 58→1, 79→2 (SC-080 spełnione), listy zagnieżdżone.
2. **US1 — „§ N”**: goły wiersz „§ 5” jako jednostka (wyróżniony, odosobniony), osłona przed wciągnięciem do tabeli
   bez siatki, wiersz z tytułem jako jeden nagłówek, poziom pod numerowanym rozdziałem.
3. **US3 — słowniczek**: region z liniami poziomymi dzielonymi na wspólnym x i etykietą + pogrubionym terminem po
   lewej → lista definicji (adnotacje `deflist.*`), nie tabela.
4. **US4 — regresja**: prywatny korpus z 15 dokumentami mBanku (punkt odniesienia przed zmianą), pomocnik miar w
   testach, pełny zestaw kontroli przed każdym commitem (FR-534).

## Technical Context

**Language/Version**: C# 13 / .NET 9 (`net9.0`), SDK z `global.json` — bez zmian

**Primary Dependencies**: bez zmian (`PdfPig`, `Microsoft.Extensions.*`)

**Storage**: N/A (prywatny korpus właściciela w `tests/LegalAgent.PdfParser.Tests/Corpus/private`, poza git)

**Testing**: xUnit v3 na MTP; testy etapów (`LayoutFactory`, `PageSketch`, `StageHarness`), repliki stron
(`SyntheticPdfBuilder`), goldeny `Corpus/acts` i `Corpus/banking`, prywatny korpus (`LEGALAGENT_PRIVATE_CORPUS`),
korpus syntetyczny (`refresh`, `verify`, `CorpusFull`)

**Target Platform**: Windows i Linux (CI ubuntu-latest); wynik deterministyczny

**Project Type**: biblioteka klas (`LegalAgent.PdfParser`) + CLI; bez zmian w aplikacjach

**Performance Goals**: bez regresji SC-007 (100 stron < 10 s); zmiany liniowe względem liczby wierszy

**Constraints**: wynik wyłącznie z tekstu PDF (bez dopisanych słów); reguły z geometrii i typografii, nie z nazw
dokumentów (FR-530); goldeny bez zmian bez zgody właściciela (FR-163/FR-531); prawdziwe PDF-y poza git (FR-533)

**Scale/Scope**: zmiany w `ListLabelPatterns`, `TableDetectionStage`, `ListDetectionStage`, `HeadingDetectionStage`
(+ `AssignLevels`), `LegalUnitPatterns`, `ReadingOrderStage` (słowniczek), `LayoutAnnotations`; 2 wartości enum;
~35 wymagań FR-500…FR-541, 8 kryteriów SC-080…SC-087

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Zasada / ograniczenie | Ocena | Jak spełnione |
|---|---|---|
| I. Test-First / TDD | ✅ | Każda zmiana zachowania: replika strony lub test etapu (red), osobny commit, potem kod (green). Testy offline; prawdziwe PDF-y tylko w prywatnym korpusie. |
| II. Wierność źródłu | ✅ | Etykiety i tytuły dosłownie, bez dopisanych znaków (także w słowniczku — decyzja właściciela); żaden tekst nie jest pomijany. |
| III. Powtarzalność | ✅ | Deterministyczne sortowania, kultura niezmienna we wzorcach; pomiar jednym pomocnikiem testowym. |
| IV. Odporność na błędy | ✅ | Brak wywołań zewnętrznych; region niespełniający warunków wraca do dotychczasowej ścieżki. |
| V. Bezpieczeństwo i konfiguracja | ✅ | Prawdziwe dokumenty i ich nazwy poza git (lista „układu etykiet” w nieśledzonym pliku). |
| VI. Prostota | ✅ | Rozszerzenie istniejących ścieżek list zamiast nowego etapu (R1); jedna nowa para adnotacji tylko dla słowniczka. Zero nowych zależności. |
| VII. Dokumentacja | ✅ | README parsera: etykiety „1/”, „a/”, „§ N”, słowniczek, zaktualizowane znane ograniczenia; handoff w planie. |
| Biblioteka ogólna | ✅ | Wzorce etykiet i „§” to ogólny skład polskich regulaminów; brak reguł dla mBanku. |
| API jawne, SemVer | ✅ | Kontrakt 1.2.0 (MINOR): dwie nowe wartości `ListLabelKind` ([contracts/markdown-output.md](./contracts/markdown-output.md)). |

**Wynik bramki (przed Phase 0)**: PASS.

**Re-check po Phase 1**: PASS — model zmienia się addytywnie ([data-model.md](./data-model.md)), bez nowych projektów i
zależności. Ryzyko regresji opisane w research (R1–R3) i osłonięte FR-534/SC-087.

## Project Structure

### Documentation (this feature)

```text
specs/007-corporate-regulation-layout/
├── plan.md              # ten plik
├── research.md          # Phase 0: R1 etykiety, R2 „§ N”, R3 słowniczek, R4 pomiar, R5 kolejność
├── data-model.md        # Phase 1: ListLabelKind, Section, adnotacje deflist.*, miary
├── quickstart.md        # Phase 1: walidacja
├── contracts/
│   └── markdown-output.md   # uzupełnienie kontraktu Markdown (1.2.0)
├── checklists/requirements.md
└── tasks.md             # /speckit-tasks
```

### Source Code (repository root)

```text
src/LegalAgent.PdfParser/
├── Model/ListItem.cs                    # ListLabelKind: ArabicSlash, LetterSlash
├── Text/ListLabelPatterns.cs            # wzorce „1/”, „a/” (cały token + tekst)
├── Text/LegalUnitPatterns.cs            # goły wiersz „§ N”
├── Layout/LayoutAnnotations.cs          # deflist.entry, deflist.side
├── Stages/TableDetectionStage.cs        # IsLabel/CellsOf, IsHangingList, osłona wiersza jednostki, słowniczek
├── Stages/ListDetectionStage.cs         # Rank, budowa elementów słowniczka
├── Stages/ReadingOrderStage.cs          # kolejność wpisów słowniczka
├── Stages/HeadingDetectionStage.cs      # goły „§ N”, tytuł bez podziału, poziom pod numerowanym rozdziałem
└── Rendering/MarkdownEscaper.cs         # etykieta „1/” bez ucieczki (sprawdzenie)

tests/LegalAgent.PdfParser.Tests/
├── Unit/Text/ListLabelPatternsTests.cs
├── Unit/Stages/HeadingDetectionStageTests.cs, TableDetectionStageTests.cs, ListDetectionStageTests.cs
├── Integration/HangingLabelLayoutTests.cs, ParagraphUnitHeadingTests.cs, GlossaryLayoutTests.cs   # repliki stron
├── Corpus/PrivateCorpusTests.cs         # + miary i progi SC (pomocnik LayoutMetrics)
└── Fixtures/LayoutMetrics.cs            # nowy pomocnik miar (R4)
```

**Structure Decision**: wyłącznie biblioteka parsera i jej testy; `LegalAgent.Chunking`, `LegalAgent.Faq` i aplikacje
bez zmian kodu (korzystają z nowych nagłówków i list automatycznie).

## Kolejność realizacji (wskazówka dla /speckit-tasks)

Harmonogram: konsultacje dzień 3, implementacja do dnia 8, zamrożenie dni 9–10.

1. **Przygotowanie (dzień 3–4)**: prywatny korpus +15 dokumentów z goldenami obecnego wyniku i lista „układu
   etykiet”; pomocnik `LayoutMetrics` + raport miar w teście prywatnego korpusu (pomiar wyjściowy jako test).
2. **US2 (dzień 4–5)**: wzorce „1/”, „a/” (red/green), `IsLabel`/`CellsOf`, `Rank`, `KeepLabelledBoldTextInLists`,
   uogólnienie `IsHangingList`; repliki R2 (zagnieżdżenie, trzy poziomy + część wspólna, kontrola tabeli danych).
   Pełny zestaw kontroli (FR-534) i przegląd różnic D-A…D-D z właścicielem.
3. **US1 (dzień 5–6)**: C1 goły „§ N” + C3; C4 tytuł; C5 poziom; C2 osłona w regionach bez siatki — każde osobno
   red/green; repliki R1a–R1d.
4. **US3 (dzień 6–8)**: słowniczek (adnotacje, ReadingOrder, ListDetection); replika R3 i wariant z „1.”.
5. **US5 (opcjonalnie, tylko jeśli dzień 8 pozwala)**: spis treści z kropkami, numery „N/M”.
6. **Zamknięcie (dzień 8)**: README parsera, CLAUDE.md, handoff, miary końcowe, przebieg FAQ na D-A…D-D (SC-083).

## Stan prac i przekazanie

- 2026-10-10: spec (z clarify: format słowniczka, reguła regresji 5+6 dokumentów, FR-534/SC-087), research R1–R5 na
  prawdziwych stronach D-A…D-D, data-model, kontrakt Markdown 1.2.0, quickstart. Następny krok: `/speckit-tasks`.
- Dane pomiarowe i narzędzia sond (PdfPig) w scratchpadzie sesji — nie są częścią repozytorium.

## Complexity Tracking

Brak naruszeń konstytucji — sekcja pusta.
