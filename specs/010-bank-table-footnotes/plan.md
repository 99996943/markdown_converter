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
- 2026-10-12, implementacja Phase 1–2 (T001–T006):
  - T001: `baseline-010/` = 21 goldenów prywatnych (1.2.3); pełny zestaw z prywatnym korpusem, `CorpusFull` i raportem
    zielony na starcie (2188 testów, 0 niepowodzeń, 2 pominięte znane).
  - T003, pomiar wyjściowy (prywatne; tabele / z liniami pionowymi / TBL001 z sondy T004; TBL001, wiersze „ \| ”,
    Liczby +/−, Odnośniki sklejone z raportu):

    | Dokument | Tabele | Z liniami | TBL001 | Wiersze „ \| ” | Liczby + | Liczby − | Odnośniki sklejone |
    |---|---|---|---|---|---|---|---|
    | corp-1 | 8 | 6 | 4 | 19 | 0 | 0 | 0 |
    | corp-2 | 0 | 0 | 0 | 0 | 8 | 1 | 0 |
    | corp-5 | 6 | 4 | 0 | 0 | 0 | 0 | 1 |
    | firm-1 | 6 | 6 | 3 | 76 | 6 | 6 | 0 |
    | firm-2 | 5 | 5 | 0 | 0 | 0 | 0 | 4 |
    | firm-3 | 10 | 10 | 0 | 0 | 0 | 0 | 9 |
    | firm-4 | 1 | 1 | 0 | 0 | 0 | 0 | 0 |
    | firm-5 | 15 | 15 | 1 | 2 | 0 | 0 | 11 |
    | firm-6 | 2 | 2 | 0 | 0 | 0 | 0 | 1 |
    | ind-1 | 9 | 9 | 0 | 0 | 0 | 0 | 15 |
    | ind-2 | 11 | 11 | 7 | 62 | 1 | 0 | 7 |
    | ind-3 | 1 | 1 | 0 | 0 | 0 | 0 | 0 |
    | ind-4 | 16 | 16 | 6 | 82 | 1 | 0 | 17 |
    | ind-5 | 1 | 1 | 0 | 0 | 0 | 0 | 0 |
    | reg1 / regulamin-pdp | 13 | 13 | 3 | 52 | 1 | 0 | 9 |
    | reg2 | 1 | 1 | 0 | 0 | 0 | 0 | 0 |
    | corp-3, corp-4, corp-6, reg3 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |

    downloads-1…5 (konwersja CLI do katalogu tymczasowego): tabele GFM 7 / 3 / 2 / 3 / 6, TBL001 0 / 0 / 1 / 0 / 2,
    wiersze „ \| ” 0 / 0 / 15 / 0 / 31, definicje przypisów 0 / 1 / 1 / 1 / 0.
  - T004, sonda zasięgu (test tymczasowy, nie zacommitowany): (b) słowa z `FootnoteId` w liniach tabel — **0** we
    wszystkich korpusach (acts, banking, `corpus/`, prywatny), więc przenoszenie `FootnoteId` w `RowDraft` (T013) nie
    zmienia samo goldenów chronionych. (a) tabele z ≥ 3 liniami pionowymi w obszarze: akt MSZ (2), banking
    `taryfa-z-siatka` (2) i `regulamin-z-tabela-definicji` (1), w `corpus/` prawie wszystkie taryfy, procedury i
    regulaminy (1–8 tabel na dokument), w prywatnym korpusie prawie wszystkie tabele. **Ryzyko**: wpięcie siatki
    (T015) dotyczy goldenów chronionych — ścieżka siatki musi dawać dla prostych pełnych siatek ten sam wynik; każda
    zmiana acts/banking/`corpus/` → stop i pytanie (FR-755).
  - T005–T006: `RuledTableGrid` (kolumny z linii pionowych w ≥ 2 wierszach, granice wierszy z odcinków ≥ 80% pasma,
    scalenia pionowe i poziome przez union-find, `Locate` po środku x i linii bazowej), 6 testów, jeszcze niewpięty.
- 2026-10-12, Phase 3 / US2 przypisy (T007–T013, T013b), parser bez zmiany wersji:
  - `FootnoteDetectionStage`: odnośnik także na początku słowa (`LayoutWord.ReferencesNextWord`, renderowane
    `[^n]kurs`); etykieta definicji z małych podniesionych cyfr (z odstępem lub bez) i — tylko pod kreską, dla numerów z
    odnośnikiem na stronie — goła cyfra ze spacją; obszar pod kreską przy rozmiarze tekstu głównego, gdy pierwsza linia
    ma taką etykietę; podniesiona etykieta na początku linii pod kreską nie jest odnośnikiem (pierwotnie pomijane było
    całe pierwsze słowo — psuło to jednowyrazową linię „podatkowego.⁴” w `corpus/zatrute/…/ZAT-REG-FAL-01`, zawężone
    przed commitem). `TableDetectionStage`: `Token` przenosi `FootnoteId`, komórki emitują `FootnoteRef`;
    `DocumentBuildStage` przenumerowuje odnośniki w tabelach (bez tego wychodziło `[^0]`).
  - T013b (nowe, poza planem): `NumberCheck.Compare(..., LegalDocument?)` — cyfry oryginalnych etykiet przypisów
    (jedna na definicję i jedna na odnośnik `[^n]`) nie są „brakujące”; bez tego „Liczby −” rosły sztucznie (np. ind-1
    0 → 17), bo etykiety „8 Dotyczy…” i odnośniki przed słowem są w PDF zwykłymi liczbami. To część T026 zrobiona
    wcześniej (dokument przekazywany już w `PrivateCorpusTests`).
  - Goldeny chronione: `corpus/akty/dz-u-2025-720-kredyt-hipoteczny` (przypis „1 Niniejszy dokument…” odklejony od
    przypisu 7 → osobny `[^8]`) i prywatne reg1 / regulamin-pdp — **zatwierdzone przez właściciela** (FR-755).
    `Corpus/acts`, `Corpus/banking`, goldeny fragmentów — bez zmian.
  - Goldeny prywatne zaktualizowane (wąska zgoda): corp-5, firm-1, firm-2, firm-3, firm-5, firm-6, ind-1, ind-2, ind-4
    (+ reg1, regulamin-pdp za zgodą). Zmiany wyłącznie w przypisach: definicje dotąd sklejone w jeden `[^1]` (etykiety
    gołą cyfrą) rozdzielone, odnośniki „3kurs”, „Tak4”, „TAK/TAK5”, „BLIK8”, „taryfa?6” → `[^n]` (także w komórkach
    tabel i — jak dotąd — usuwane z tekstu nagłówków). Porównanie słów vs `baseline-010`: utracone wyłącznie tokeny
    sklejone z cyfrą i etykiety definicji (po normalizacji sklejonych cyfr: brak innych różnic). Miary przed → po:

    | Dokument | Liczby + | Liczby − | Odnośniki sklejone |
    |---|---|---|---|
    | corp-5 | 0 → 0 | 0 → 0 | 1 → 0 |
    | firm-1 | 6 → 5 | 6 → 4 | 0 → 0 |
    | firm-2 | 0 → 0 | 0 → 0 | 4 → 0 |
    | firm-3 | 0 → 0 | 0 → 0 | 9 → 1 |
    | firm-5 | 0 → 0 | 0 → 0 | 11 → 6 |
    | firm-6 | 0 → 0 | 0 → 0 | 1 → 0 |
    | ind-1 | 0 → 0 | 0 → 0 | 15 → 0 |
    | ind-2 | 1 → 0 | 0 → 0 | 7 → 0 |
    | ind-4 | 1 → 0 | 0 → 0 | 17 → 4 |
    | reg1 / regulamin-pdp | 1 → 0 | 0 → 0 | 9 → 0 |

    W `number-check.txt` usunięte dopuszczenia ind-2 „3” i ind-4 „1” (bramka zielona bez nich). Pozostałe sklejone:
    „BLIK9,10” → „BLIK9,[^n]” (odnośnik z przecinkiem — poza zakresem, „Znane ograniczenia”), część firm-5/ind-4
    (odnośniki w komórkach tabel bez siatki lub definicje bez etykiety — do sprawdzenia po Phase 4–6).
  - Pełny zestaw z prywatnym korpusem i `CorpusFull`: 2205 testów, 0 niepowodzeń; `verify` czysty.
- 2026-10-12, T013a (sonda geometrii, PdfPig w katalogu tymczasowym): **wszystkie badane tabele są w pełni z liniami**
  — downloads-3 s. 17–18 (5 kolumn, linie pionowe 59,0/204,5/288/371/456/555,5; nagłówek 11 pt w 5 liniach, powtórzony
  na s. 18 ze zmienionym tekstem kolumny 1; wartości wyśrodkowane, odnośniki 7 pt +4 pt „Tak¹³”), downloads-4 s. 16–17
  (5 kolumn, 42,5/184,5/275/365,5/459,5/553; nagłówek powtórzony), downloads-5 s. 6–8 (5 kolumn 36/142,5/241,5/355/
  454,5/559, przez 3 strony, nagłówek powtórzony; etykiety kolumny 1 do 7 linii) i s. 26–27 (5 kolumn 36/168/288/376/
  464/559, nagłówek 7 linii; etykiety kolumny 1 zaczynają się dokładnie na linii pionowej x 36), firm-5 s. 22–23
  (4 kolumny 45/286/399,5/477,5/555,5; wiersz grupujący „Gdzie możesz zrobić?” nad kolumnami 2–4 — linia 475,5 tylko
  nad nimi, linie pionowe 399,5 i 477,5 zaczynają się pod grupą, kolumna 1 scalona przez oba wiersze nagłówka; s. 23
  bez powtórzonego nagłówka). Wariantów bez linii w zakresie przypadków nie ma: repliki P3 bez linii w T014 zbędne,
  T029 pozostaje próbą na syntetycznej macierzy bez linii.
- 2026-10-12, Phase 4 / US3 (T014–T017): `HeaderGroup` w modelu i rendererze (T016–T017, bez zmian goldenów);
  T014 repliki (downloads-2 s. 20 + test ochronny taryfy z siatką); T015 — `TableDetectionStage.FromRuledGrid`: region
  w siatce z liniami pionowymi (`Grid.Around` daje granice nad pierwszą i pod ostatnią linią) budowany z
  `RuledTableGrid`; słowa linii przypisywane fragmentami (segmenty linii dzielone na granicy kolumny tylko w odstępie
  ≥ 1,25 × mediana pozostałych spacji segmentu, z tolerancją 2 pt na glif dotykający linii, i tylko między różnymi
  komórkami siatki) według środka fragmentu; wiersz sekcji przez całą szerokość — kolumna środka tekstu (jak dotąd
  w akcie MSZ); tekst komórki scalonej idzie do jej pierwszego wiersza (scalenia pionowe — T024). Próby po drodze:
  przypisanie słowo po słowie rozbijało tekst wychodzący poza linię (lista kontrolna spec 007) i tytuły sekcji;
  sam segment według środka sklejał nagłówki firm-3 (odstęp 6,8 pt przy medianie 4,8 pt).
  - Goldeny: acts, banking, `corpus/` bez zmian; prywatne zaktualizowane — TBL001 corp-1 4→1, firm-1 3→0, firm-5 1→0,
    ind-2 7→0, ind-4 6→0, reg1/regulamin-pdp 3→0; wiersze „ \| ” 19→2, 76→0, 2→0, 62→0, 82→0, 52→0; liczby bez
    zmian. Porównanie słów vs stan po US2: utracone wyłącznie słowa powtórzonych nagłówków tabel na kolejnych
    stronach (ind-2 70, ind-4 84, reg1 41, firm-3 7), nowych słów brak. reg1/regulamin-pdp — diff pokazany i
    **zatwierdzony przez właściciela**. Znane resztki: wiersze z pustymi kolumnami 2…k na początku strony (komórka
    urwana na końcu strony — T033), jeden wiersz tabeli limitów ze znakami ✓ w firm-1 (s. 3) nadal niedoskonały.
  - Pełny zestaw: 2210 testów, 0 niepowodzeń; `verify` czysty.
- 2026-10-12, T018–T019: wiersz grupujący — pierwszy wiersz siatki z komórką przez część kolumn i pozostałymi
  komórkami scalonymi w dół z wierszem nazw kolumn, tekst obu wierszy pogrubiony → `HeaderGroup`; komórki scalone w dół
  należą do nazw kolumn. `DocumentBuildStage` numeruje odnośniki w `HeaderGroup`, `FootnoteSelector` chunkera dołącza
  ich definicje do każdej części tabeli (test w `FootnoteSelectionTests`). Prywatnie zmienił się tylko firm-6 (macierz
  „Co można zrobić? / Gdzie można zrobić?” — przypadek opisany w research jako „firm-3 s. 8”). Macierz firm-5 s. 22–23
  (grupa „Gdzie możesz to zrobić?”) nadal zła: wysoki nagłówek poza regionem (staje się `###`), s. 23 rozsypana —
  zakres T027–T028. Wariant z wierszem grupującym przez dwie strony dopisany zostanie do T032/T033 (łączenie).
  - Punkt kontrolny Phase 4 (downloads-1…5, CLI do katalogu tymczasowego): TBL001 0/0/0/0/0 (było 0/0/1/0/2), wiersze
    „ \| ” 0 wszędzie (było 15 w downloads-3 i 31 w downloads-5), definicje przypisów 0/25/20/3/0 (było 0/1/1/1/0),
    tabele GFM 7/3/3/2/7 (downloads-4: części tabeli połączone przez strony). downloads-2 s. 21 — macierz Private
    Banking ma 6 kolumn (z „oddział”) i tak jest wyprowadzana.
  - Pełny zestaw: 2213 testów, 0 niepowodzeń; `verify` czysty.
- 2026-10-12, Phase 5 / US1 scalone wiersze (T020–T026):
  - T020–T021: `TableCell(Inlines, ColumnSpan, RowSpan = 1, ContinuesAbove = false)`; renderer bez zmian (test jako
    regresja, K1). T022: test chunkera (część od środka grupy ma etykietę) — regresja bez zmian kodu.
  - T023–T024: komórka siatki bez linii na granicy wiersza w swoim paśmie (przy linii w innym) → `RowSpan` w pierwszym
    wierszu i komórki `ContinuesAbove` z tą samą listą inline'ów w kolejnych (`RowDraft.SetRowSpan`/`ContinueFrom`);
    nazwa kolumny nigdy nie jest scalana w dół (wiersze pod nią dostają pustą komórkę); `DocumentBuildStage` przy
    przenumerowaniu odnośników zachowuje wspólną listę.
  - T025–T026: `NumberCheck` z dokumentem odejmuje liczby komórek `ContinuesAbove` i komórek `HeaderGroup`
    × (objęte kolumny − 1) (FR-758). Porównanie słów w raporcie działa na zbiorach — powtórzenia go nie zmieniają.
  - SC-110: firm-5 s. 13 — etykiety „przelew wychodzący SWIFT” (3 wiersze) i „przelew wychodzący (wewnętrzny)”
    (3 wiersze) w każdym wierszu grupy; podobnie inne tabele firm-5 (przelew SWIFT/SEPA, waluta obca) i firm-1 (BLIK).
    corp-1: tabela tagów SWIFT (T10, poza zakresem) — komórki scalone powtarzane w objętych wierszach, **zatwierdzone
    przez właściciela**. Liczby bez zmian (bramka zielona). Pełny zestaw: 2218 testów, 0 niepowodzeń; `verify` czysty.
- 2026-10-12, Phase 6 / US4 macierz czynności (T027–T029, T028a):
  - Uwaga do nazw: „firm-1 s. 16–17” z PRZYPADKI.md to w prywatnym korpusie **firm-2** s. 16–17.
  - T027: repliki (a) ind-4 s. 42–43 (wiersze blisko, powtórzony nagłówek) — zielona bez zmian kodu (regresja, K1);
    (b) firm-2 s. 16–17 (wiersze co 25 pt > 2,5 interlinii, nagłówek 3-liniowy w kolumnie 2, „TAK/TAK⁵”, ciąg dalszy
    na s. 17 bez powtórzonego nagłówka) — czerwona.
  - T028: `GrowRegion` dołącza linie w tym samym lub sąsiednim paśmie siatki (`Grid.Connects`) bez względu na odstęp;
    pusty pasek siatki (wiersz odstępu między dwiema tabelami formularza, akt dz-u-2024-1497 s. 27) nadal rozdziela
    tabele — pierwsza wersja bez warunku sąsiedztwa sklejała je (golden aktu, wycofane). W `Build` linie wieloczęściowe
    nad ziarnem w tej samej siatce należą do nagłówka, a reguła „tabela bez siatki zaczyna się od pogrubionego
    nagłówka” nie dotyczy siatki. Osobna para red/green: wiersz grupujący nad nazwami kolumn, które zaczynają się
    pogrubioną linią, a dalej mają objaśnienie zwykłym krojem (firm-5 s. 22 „(nasza strona internetowa/ aplikacja
    mobilna)”) → `HeaderGroup`; nagłówek tabeli z grupą jest nagłówkiem także bez pełnego pogrubienia.
  - T028a (dodane): etykieta definicji przypisu podniesiona i złożona jako osobna linia nachodząca na linię tekstu
    (firm-5 s. 22–23 „13”, „14”, „19”) jest etykietą tej linii (`LoneLabel`) — odnośniki „TAK13”, „TAK14”, „BLIK19”,
    „BLIK1” (słownik s. 3) stają się `[^n]`, definicje nie są doklejane do poprzednich. Stopka banku doklejona do
    ostatniego przypisu strony (wcześniej do innego) — znane ograniczenie (stopka przeplatana).
  - Goldeny prywatne (wąska zgoda FR-755, tylko tabele i przypisy): firm-2 — macierz s. 16–17 jedną tabelą z nagłówkiem
    „**serwis transakcyjny** (na naszej stronie internetowej lub w aplikacji mobilnej)” zamiast `###` i akapitów;
    firm-5 — macierz s. 22–23 jedną tabelą (wcześniej `###`, 3 tabele i akapity) z wierszem grupującym „Gdzie możesz
    to zrobić?” przed nazwami kolumn, przypisy jw. `Corpus/acts`, `Corpus/banking`, `corpus/` bez zmian.
  - downloads-1…5: TBL001 0, `###` 0; downloads-1 — tabela definicji s. 2–3 jedną tabelą (wcześniej akapity i
    powtórzony nagłówek), komórka „Gdzie sprawdzisz…” urwana w dwóch wierszach siatki powtarza etykietę scalenia
    (do T033); downloads-2…5 bez zmian.
  - T029: macierz bez linii daje format zapasowy (linie nagłówka o różnej liczbie komórek, wiersze wartości osobno
    od linii kolumny 1). Wiersze trzeba by wyznaczać z symetrii linii kolumny 1 wokół linii wartości — heurystyka
    ryzykowna dla taryf i słowników bez siatki, a sonda T013a nie znalazła prawdziwej macierzy bez linii. Test
    `Skip` z odwołaniem do „Znanych ograniczeń” (README w T039).
  - SC-113: ind-4, ind-2, firm-2, firm-5, downloads-3, downloads-5 — 0 linii TBL001 i 0 wierszy macierzy jako akapity.
    Pełny zestaw: 2223 testy, 0 niepowodzeń, 1 pominięty (T029) + dotychczasowe pominięcia; `CorpusFull` zielony.

## Complexity Tracking

Brak naruszeń konstytucji — sekcja pusta.
