# Implementation Plan: Oryginalne etykiety przypisów w Markdownie (parser 1.4.0)

**Branch**: `012-footnote-original-labels` (od `011-ftn001-false-warnings`, parser 1.3.1) | **Date**: 2026-10-13 |
**Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/012-footnote-original-labels/spec.md`

## Summary

Identyfikator przypisu w Markdownie przestaje być numerem nadanym przez parser i staje się etykietą z PDF-u, zapisaną
dosłownie (`[^21]`, `[^1)]`, `[^*)]`). Powtórzona etykieta dostaje dopisek numeru wystąpienia (`[^1)-2]`), przypis
bez etykiety — numer zastępczy i ostrzeżenie FTN002. `DocumentBuildStage` wylicza identyfikator raz dla dokumentu i
zapisuje go w nowych właściwościach `Footnote.Identifier` i `FootnoteRef.Identifier`; renderer (a z nim chunker) je
wypisuje. Kolejność i miejsce definicji bez zmian. Research R2 pokazał, że „przeskakująca numeracja” z przykładu to
skutek 17 nierozpoznanych odnośników (spec 011 K1/K2), nie kolejności tabel — znika razem z numerami parsera.

## Technical Context

**Language/Version**: C# 13 / .NET 9 (`net9.0`), SDK z `global.json` — bez zmian

**Primary Dependencies**: bez zmian (`PdfPig` 0.1.16); bez biblioteki Markdown (R3: reguły GFM z dokumentacji,
weryfikacja podglądem)

**Storage**: N/A

**Testing**: xUnit v3 na MTP; `Unit/Stages/DocumentBuildStageTests` (reguła identyfikatora na modelu), testy
renderera (`Rendering/`), replika regulaminu i aktu w `Integration/` (`SyntheticPdfBuilder`), chunker
(`tests/LegalAgent.Chunking.Tests`), `NumberCheck`; goldeny parsera, chunków i `corpus/` zmieniają się (akceptacja
właściciela)

**Target Platform**: Windows i Linux (CI ubuntu-latest); wynik deterministyczny

**Project Type**: biblioteka klas `LegalAgent.PdfParser` + CLI; chunker bez zmian kodu poza testami (renderer wspólny)

**Performance Goals**: bez regresji (jedno przejście po przypisach dokumentu)

**Constraints**: tylko tekst z PDF-u (identyfikator zastępczy i dopisek wyłącznie w przypadkach FR-903/FR-904);
publiczne API zgodne wstecz; liczba przypisów, odnośników i FTN001 bez zmian; poza identyfikatorami 0 różnic w
Markdownie i chunkach (SC-133)

**Scale/Scope**: `Model/Footnote.cs`, `Model/Inline.cs`, `Stages/DocumentBuildStage.cs`, `Rendering/MarkdownRenderer.cs`,
`Fixtures/NumberCheck.cs`, nowy kod ostrzeżenia FTN002; README, kontrakt 2.0.0, CLAUDE.md; 13 wymagań FR-900…FR-912,
5 kryteriów SC-130…SC-134

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Zasada / ograniczenie | Ocena | Jak spełnione |
|---|---|---|
| I. Test-First / TDD | ✅ | Testy reguły identyfikatora, renderera, replik, niezmiennika SC-130 i chunkera jako commit red; potem green; goldeny osobnym commitem po akceptacji właściciela. Odstępstwo: puste właściwości `Identifier` (bez zachowania) powstają przed testami, bo bez nich testy red się nie kompilują — w tym samym commicie red (tasks T003). |
| II. Wierność źródłu | ✅ | Usuwa z wyniku numerację parsera; wyjątki (dopisek, zastępczy) jawne w kontrakcie i raporcie (FTN002). |
| III. Powtarzalność | ✅ | Reguła deterministyczna (kolejność stron, id szkicu); `generate`/`verify` korpusu. |
| IV. Odporność na błędy | ✅ | Brak wywołań zewnętrznych; ostrzeżenie nie zmienia kodu wyjścia. |
| V. Bezpieczeństwo i konfiguracja | ✅ | Repliki bez treści prywatnych dokumentów; prywatne PDF-y poza git. |
| VI. Prostota | ✅ | Dwie właściwości `init`, jedna funkcja w `DocumentBuildStage`; bez nowych zależności i opcji. |
| VII. Dokumentacja | ✅ | Kontrakt 2.0.0, README parsera (1.4.0, FTN002), CLAUDE.md, handoff. |
| API jawne, SemVer | ✅ | API addytywne → biblioteka 1.4.0 (MINOR); format Markdown → kontrakt 2.0.0 (MAJOR), opisany w README. |

**Wynik bramki (przed Phase 0)**: PASS. **Re-check po Phase 1**: PASS — model addytywny ([data-model.md](./data-model.md)),
kontrakt [contracts/markdown-output.md](./contracts/markdown-output.md).

## Project Structure

### Documentation (this feature)

```text
specs/012-footnote-original-labels/
├── plan.md              # ten plik
├── research.md          # R1 etykiety w korpusie, R2 przyczyna przeskoków, R3 GFM, R4 model, R5 dopisek, R6 testy
├── data-model.md        # Footnote.Identifier, FootnoteRef.Identifier, reguła, FTN002
├── quickstart.md        # weryfikacja
├── contracts/
│   └── markdown-output.md   # kontrakt 2.0.0: identyfikatory przypisów
├── checklists/requirements.md
└── tasks.md             # /speckit-tasks
```

### Source Code (repository root)

```text
src/LegalAgent.PdfParser/
├── Model/Footnote.cs, Model/Inline.cs       # Identifier (init)
├── Stages/DocumentBuildStage.cs             # AssignIdentifiers po NumberFootnotes; Renumber przenosi Identifier
├── Rendering/MarkdownRenderer.cs            # [^Identifier], zapas: Number
└── README.md                                # 1.4.0, kontrakt 2.0.0, FTN002
Directory.Build.props                        # 1.4.0

tests/LegalAgent.PdfParser.Tests/
├── Unit/Stages/DocumentBuildStageTests.cs   # reguła identyfikatora (etykieta, dopisek, zastępczy, kolizje)
├── Rendering/…                              # renderer: Identifier i zapas Number
├── Integration/FootnoteLabelsIntegrationTests.cs   # repliki: regulamin z tabelą, akt z dwiema seriami
├── Fixtures/NumberCheck.cs                  # wzorce identyfikatorów
└── Corpus/**/*.expected.md                  # zmiana po akceptacji
tests/LegalAgent.Chunking.Tests/             # chunk z [^21]; goldeny chunków po akceptacji
corpus/                                      # generate/verify
```

**Structure Decision**: bez nowych projektów.

## Kolejność realizacji (wskazówka dla /speckit-tasks)

1. **Setup**: pomiar 1.3.1 (Markdown, chunki, liczby przypisów/odnośników/FTN001) wszystkich dokumentów — punkt
   odniesienia dla SC-131/SC-133; skrypt normalizacji identyfikatorów do porównania.
2. **US1 red**: testy reguły w `DocumentBuildStageTests`, renderera, replik (`SyntheticPdfBuilder`); commit red.
3. **US1 green**: model, `DocumentBuildStage`, renderer, FTN002, `NumberCheck`; testy jednostkowe zielone, goldeny
   padają → raport diffu po normalizacji (SC-133) → **stop: akceptacja właściciela** → `UPDATE_GOLDEN=1`, commit.
4. **US2**: test kolejności definicji (akapit przed tabelą, sieroty po stronach) — oczekiwane zielone od razu
   (research R2); jeśli nie, red/green.
5. **US3**: chunker — test części z `[^21]` (test regresji); goldeny chunków zmieniają się w US1, razem z goldenami
   parsera, jedną akceptacją właściciela.
6. **Polish**: podgląd GFM (VS Code, GitHub), wersja 1.4.0, `generate`/`verify`, README, kontrakt, CLAUDE.md,
   `publish/`, handoff.

## Stan prac i przekazanie

- 2026-10-13: spec (decyzje właściciela: etykieta dosłownie, dopisek `-k` dla powtórzeń; linki FAQ → spec 013),
  plan z research R1–R6. Pomiar etykiet sondą tymczasową (usunięta). Następny krok: `/speckit-tasks`.
- Kandydat o większym zysku jakości niż ta spec: rozpoznawanie odnośników z pełnowymiarową interpunkcją (w
  regulaminie kredytu gotówkowego 17 z 25 przypisów bez odnośnika; spec 011 K1/K2).

## Complexity Tracking

Brak naruszeń konstytucji.
