# Implementation Plan: Poprawki tabel i przypisów parsera na regulaminach bankowych

**Branch**: `010-bank-table-footnotes` (od `main` po scaleniu PR #12, #13; wersja 1.2.3) | **Date**: 2026-10-12 |
**Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/010-bank-table-footnotes/spec.md`

## Summary

Poprawki `TableDetectionStage` i `FootnoteDetectionStage` dla tabel i przypisów regulaminów bankowych, zbadane
sondą PdfPig na prywatnych PDF-ach i przeglądem kodu ([research.md](./research.md)). Kluczowe ustalenie: przypadki
T1, T2, T3, T5, T6 to tabele **w pełni z liniami** — kolumny psuje wyznaczanie ich z lewych krawędzi wyśrodkowanego
tekstu, a wiersze i scalenia — linie poziome rozpatrywane bez kolumn.

1. **Siatka tabel z liniami** (R1, R2): nowy `RuledTableGrid` — kolumny z linii pionowych, granice wierszy osobno dla
   każdej kolumny, tekst przypisywany do komórek słowo po słowie według geometrii (środek x, linia bazowa). Naprawia
   T3 (nagłówek w kilku liniach), T1/T2 (macierz czynności), T5 (mała tabela z wyśrodkowanymi komórkami), T4 i daje
   scalenie pionowe T11. Tabele bez siatki — dotychczasowa ścieżka.
2. **Model** (R3, R4): `TableCell.RowSpan` + `TableCell.ContinuesAbove` (kontynuacje z tym samym tekstem w objętych
   wierszach — renderer i chunker bez zmian logiki), `TableBlock.HeaderGroup` (wiersz grupujący, tekst na początku
   objętych nagłówków). Wersja 1.3.0, kontrakt 1.4.0.
3. **Przypisy** (R5): odnośnik na początku słowa, odnośniki zachowane w komórkach tabel (dziś `FootnoteId` ginie),
   obszar pod kreską przy rozmiarze tekstu głównego z etykietami gołą cyfrą (podniesioną lub nie) dla numerów z
   odnośnikami na stronie; koniec zlepiania definicji w jeden `[^1]`.
4. **Strony** (R6): powtórzony nagłówek pomijany przy łączeniu (porównanie po normalizacji), wiersz-kontynuacja
   doklejany do urwanej komórki.
5. **Kontrola liczb** (R7): `NumberCheck` odejmuje powtórzenia ze scaleń i wiersza grupującego.

## Technical Context

**Language/Version**: C# 13 / .NET 9 (`net9.0`), SDK z `global.json` — bez zmian

**Primary Dependencies**: bez zmian (`PdfPig` 0.1.16); Docling tylko jako wzorzec kształtu poza repozytorium

**Storage**: N/A (prywatny korpus w `tests/LegalAgent.PdfParser.Tests/Corpus/private`, poza git; nowy nieśledzony
`baseline-010/`)

**Testing**: xUnit v3 na MTP; testy etapów (`TableDetectionStageTests`, `FootnoteDetectionStageTests`), nowy
`RuledTableGridTests`, repliki stron w nowym `Integration/BankTablesIntegrationTests` (`SyntheticPdfBuilder`: `HLine`,
`VLine` z dowolnymi odcinkami, `Text` z rozmiarem — odnośniki jako mniejszy `Text` na podniesionej linii bazowej),
`MarkdownRendererTableTests`, `SplitTablesTests` (chunker), `NumberCheckTests`; goldeny `Corpus/acts`,
`Corpus/banking`, fragmentów, prywatny korpus, korpus syntetyczny (`refresh`, `verify`, `CorpusFull`)

**Target Platform**: Windows i Linux (CI ubuntu-latest); wynik deterministyczny

**Project Type**: biblioteka klas (`LegalAgent.PdfParser`) + CLI; `LegalAgent.Chunking` bez zmian kodu (test
regresji), aplikacje bez zmian

**Performance Goals**: bez regresji SC-007 (100 stron < 10 s); siatka liniowa po posortowaniu odcinków strony

**Constraints**: wynik wyłącznie z tekstu PDF (FR-750; dozwolone powtórzenia: scalenia i wiersz grupujący); reguły z
geometrii i typografii (FR-751); goldeny według FR-755 (wąska zgoda dla prywatnych corp-*/firm-*/ind-* w tabelach i
przypisach T1–T12; reszta — stop i pytanie); prawdziwe PDF-y i nazwy banków poza git

**Scale/Scope**: `TableDetectionStage` (ścieżka siatki, `RowDraft`/`Token`, `Append`/`MergeAcrossPages`), nowy
`RuledTableGrid`, `FootnoteDetectionStage` (`FootnoteArea`, `DefinitionLabel`, `FindMarkers`, `LinkMarkers`,
`AddLine`), `BlockAssemblyStage.InlineAccumulator` (odnośnik przed słowem), `MarkdownRenderer` (`HeaderGroup`), model
(`TableCell`, `TableBlock`), `NumberCheck`; ~40 wymagań FR-700…FR-758, 8 kryteriów SC-110…SC-117

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Zasada / ograniczenie | Ocena | Jak spełnione |
|---|---|---|
| I. Test-First / TDD | ✅ | Każdy przypadek: replika strony (`SyntheticPdfBuilder`) i/lub test etapu jako osobny commit red → poprawka green. `RuledTableGrid` z testami jednostkowymi przed kodem; `NumberCheck` — test z liczbą w scalonej komórce przed zmianą. Testy offline. |
| II. Wierność źródłu | ✅ | Tylko tekst z PDF; powtórzenie tekstu komórki scalonej i wiersza grupującego to decyzje właściciela (tekst z PDF w każdym objętym miejscu), model zapisuje scalenie jawnie. Porównanie słów i kontrola liczb odejmują powtórzenia i wykazują utratę lub dopisanie. |
| III. Powtarzalność | ✅ | Deterministyczne sortowanie odcinków i słów, stałe progi; `refresh`/`verify` czyste. |
| IV. Odporność na błędy | ✅ | Brak wywołań zewnętrznych; tabela, dla której siatka jest niespójna (np. linie pionowe tylko w części wierszy), wraca na dotychczasową ścieżkę. |
| V. Bezpieczeństwo i konfiguracja | ✅ | Repliki bez treści i nazw prawdziwego banku; `baseline-010/` i PDF-y poza git; wyniki Doclinga poza repozytorium. |
| VI. Prostota i minimalne zależności | ✅ | Bez nowych zależności, opcji i etapów; jeden nowy pomocnik (`RuledTableGrid`) zamiast rozbudowy klastrowania; model rozszerzony polami z wartościami domyślnymi. |
| VII. Dokumentacja | ✅ | Kontrakt 1.4.0 ([contracts/markdown-output.md](./contracts/markdown-output.md)), README parsera (zmiany, „Znane ograniczenia”), CLAUDE.md, handoff. |
| Biblioteka ogólna | ✅ | Reguły z linii, położenia i typografii; bez nazw dokumentów i treści nagłówków. |
| API jawne, SemVer | ✅ | Nowe pola publicznego modelu (zgodne źródłowo — parametry domyślne na końcu) → 1.3.0 (MINOR); kontrakt 1.4.0. |

**Wynik bramki (przed Phase 0)**: PASS.

**Re-check po Phase 1**: PASS — model rozszerzony bez łamania niezmiennika kolumn ([data-model.md](./data-model.md));
chunker bez zmian logiki dzięki komórkom-kontynuacjom (R3); ryzyko szerokich zmian goldenów chronionych przez siatkę
(R1/R2) i odnośniki w komórkach (R5 pkt 5) ma krok sondy przed zmianą i ścieżkę FR-755.

## Project Structure

### Documentation (this feature)

```text
specs/010-bank-table-footnotes/
├── plan.md              # ten plik
├── research.md          # Phase 0: R1 kolumny z linii, R2 wiersze i scalenia, R3 model/chunker, R4 wiersz grupujący,
│                        #          R5 przypisy, R6 strony, R7 kontrola liczb, R8 wersje/kolejność/ryzyka
├── data-model.md        # Phase 1: TableCell, TableBlock, RuledTableGrid, przypisy, NumberCheck
├── quickstart.md        # Phase 1: walidacja
├── contracts/
│   └── markdown-output.md   # uzupełnienie kontraktu 1.4.0
├── checklists/requirements.md
└── tasks.md             # /speckit-tasks
```

### Source Code (repository root)

```text
src/LegalAgent.PdfParser/
├── Model/TableCell.cs                   # + RowSpan, ContinuesAbove
├── Model/ContentBlock.cs                # TableBlock + HeaderGroup
├── Model/TableRow.cs                    # dokumentacja niezmiennika
├── Stages/RuledTableGrid.cs             # nowy: kolumny z linii pionowych, granice wierszy per kolumna, Locate(word)
├── Stages/TableDetectionStage.cs        # ścieżka siatki w Build; RowDraft/Token z FootnoteId; HeaderGroup;
│                                        # Append: nagłówek po normalizacji, kontynuacja komórki; próg wierszy przez strony
├── Stages/FootnoteDetectionStage.cs     # obszar pod kreską przy rozmiarze tekstu, etykiety geometryczne/gołe cyfry,
│                                        # odnośnik-prefiks, wykluczenie etykiet obszaru z odnośników
├── Stages/BlockAssemblyStage.cs         # InlineAccumulator: FootnoteRef przed słowem
└── Rendering/MarkdownRenderer.cs        # nagłówek GFM z tekstem HeaderGroup

tests/LegalAgent.PdfParser.Tests/
├── Unit/Stages/RuledTableGridTests.cs           # nowy
├── Unit/Stages/TableDetectionStageTests.cs      # scalenia, kontynuacje, nagłówek po normalizacji
├── Unit/Stages/FootnoteDetectionStageTests.cs   # prefiks, gołe cyfry pod kreską, rozmiar tekstu, ochrona liczb
├── Rendering/MarkdownRendererTableTests.cs      # RowSpan/ContinuesAbove, HeaderGroup
├── Integration/BankTablesIntegrationTests.cs    # nowy: repliki T11, T12, T3 (+ grupa), T1/T2, T4, T5, T6
├── Fixtures/NumberCheck.cs                      # Compare(..., LegalDocument?) — odejmowanie powtórzeń
├── Unit/Fixtures/NumberCheckTests.cs            # liczba w scalonej komórce
└── Corpus/PrivateCorpusTests.cs                 # przekazanie Result.Document do NumberCheck

tests/LegalAgent.Chunking.Tests/Unit/SplitTablesTests.cs   # część od środka grupy ma etykietę (bez zmian kodu chunkera)
Directory.Build.props                                  # 1.3.0
corpus/manifest.json (+ ewentualne zmiany corpus/)     # refresh
src/LegalAgent.PdfParser/README.md, CLAUDE.md          # dokumentacja
```

**Structure Decision**: jak w spec 007–009 — poprawki w istniejących etapach biblioteki; nowy pomocnik siatki obok
`GlossaryDetection` w `Stages/`; repliki w nowej klasie testów integracyjnych, żeby nie rozbudowywać
`CorpusLayoutsIntegrationTests`.

## Kolejność realizacji (wskazówka dla /speckit-tasks)

1. **Przygotowanie**: `baseline-010/` (kopia prywatnych goldenów), raport prywatnego korpusu porównuje z
   `baseline-010`; pomiar wyjściowy (kolumna „Odnośniki sklejone”, TBL001, liczba tabel) w handoffie.
2. **Sonda zasięgu** (bez zmian zachowania): ile tabel z siatką i ile odnośników w komórkach tabel jest w
   `Corpus/acts`, `Corpus/banking`, korpusie syntetycznym i prywatnych goldenach — wynik w handoffie, ocena ryzyka.
3. **US2 przypisy** (R5 pkt 1–4): testy etapu + replika T12 bez tabeli (red) → `FootnoteDetectionStage`,
   `InlineAccumulator` (green). Potem osobno pkt 5 (odnośniki w komórkach): test (red) → `RowDraft` (green); goldeny
   chronione — sonda z kroku 2, stop i pytanie, jeśli się zmieniają.
4. **US3 + US4 siatka** (R1, R2 bez scaleń): `RuledTableGridTests` (red) → `RuledTableGrid` (green); repliki T3
   (dwa warianty nagłówka), T1/T2 (macierz przez dwie strony, nagłówek 3-liniowy, odnośniki „TAK/TAK⁵”), T5, T4 (red)
   → ścieżka siatki w `TableDetectionStage` (green). Goldeny ind-*, firm-* (wąska zgoda), reszta — stop.
5. **US1 scalenia + model** (R2, R3): test modelu/renderera (red) → `TableCell` (green); replika T11 (red) → scalenia
   w siatce (green); test chunkera (część od środka grupy). **R7** `NumberCheck` z liczbą w scalonej komórce (red →
   green) przed aktualizacją goldenów. **R4** wiersz grupujący: replika firm-3 (red) → `HeaderGroup` + renderer
   (green).
6. **US5 strony** (R6): repliki T6 (powtórzony nagłówek + urwana lista) i ind-2 s. 6–7 (red) → `Append`/łączenie
   (green). Warianty bez linii (P3): repliki macierzy i małej tabeli bez linii — jeśli poprawka pogarsza taryfy lub
   słowniki, wpis w „Znanych ograniczeniach”.
7. **Zamknięcie**: `<Version>` 1.3.0; `refresh` + `verify`; przegląd downloads-1…5 i tabela SC-110…SC-114 w
   handoffie; porównanie słów (SC-115); testy wydajności; README, CLAUDE.md, kontrakt, publish/, przebieg generatora
   FAQ (SC-117).

## Stan prac i przekazanie

- 2026-10-12: spec (decyzja właściciela: powtarzanie tekstu scalonej komórki; wiersz grupujący na początku objętych
  nagłówków), clarify (wąska zgoda na goldeny prywatne corp-*/firm-*/ind-* w tabelach i przypisach T1–T12), plan,
  research R1–R8, data-model, kontrakt 1.4.0, quickstart. Korekty spec po research: przypadki T1–T3, T5, T6 są z
  liniami (warianty bez linii → P3), model scalenia z komórkami-kontynuacjami (FR-702). Gałąź
  `010-bank-table-footnotes` bez commitów. `tasks.md`: 43 zadania (T001–T042 + T013a). `/speckit-analyze`: 0 krytycznych; poprawki naniesione (encja
  scalenia, FR-743 doklejanie tylko przy tekście w jednej kolumnie, FR-742 z wierszem grupującym, FR-710 definicja na
  tej samej albo dalszej stronie, progi kreski w FR-712, SC-112 z „Znanymi ograniczeniami”, sonda T013a dla
  niezbadanych tabel T3/T1, drobne w T002, T011, T012, T018, T022, T030). Następny krok: `/speckit-implement` od T001.

## Complexity Tracking

Brak naruszeń konstytucji — sekcja pusta.
