# Implementation Plan: Poprawki parsera po spec 007

**Branch**: `008-parser-fixes-after-007` (od `main` po scaleniu PR #10) | **Date**: 2026-10-10 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/008-parser-fixes-after-007/spec.md`

## Summary

Cztery usterki z handoffu spec 007, zbadane na prywatnych PDF-ach i całym korpusie ([research.md](./research.md)).
Wszystkie poprawki rozszerzają istniejące etapy (żadnego nowego etapu):

1. **US1 — stopki „N/M”** (R1): wzorzec już jest, ale stopka ind-2/firm-1 leży na 0,905 H, tuż nad strefą marginesu
   (0,92 H). Osobny przebieg w `ArtifactRemovalStage`: samodzielny wiersz `N/M`, najniższy/najwyższy na stronie, w pasie
   15%, ciąg na ≥ 2 stronach z M = liczba stron i stałym przesunięciem N. Usuwa 35 + 6 stopek, nic więcej w korpusie.
2. **US2 — fałszywe nagłówki** (R2): (a) „Rozdział I (…)” w D-A to zawinięty wiersz elementu „1.” w kolumnie tekstu za
   etykietą; reguła „wiersz kontynuacji” (ten sam rozmiar i pogrubienie, także słowa oznaczenia, odstęp ≤ 1,2 ×
   interlinia, poprzedni wiersz nie kończy zdania, lewa krawędź jak poprzedni wiersz albo jego tekst za etykietą) w
   `ListDetectionStage` i `HeadingDetectionStage`. Skan 4 869 nagłówków jednostek: jedno trafienie (D-A). (b) termin
   „15/ …/” jest już poprawny po spec 007 — tylko test regresji.
3. **US3 — „a.”, „i.”** (R3): nowe `ListLabelKind.LetterDot`, `RomanDot` z bramką ciągu na poziomie dokumentu
   (uogólnione `AcceptArabicDotSequences`), zagnieżdżenie z wcięcia; 10 ciągów / 31 etykiet w corp-1, corp-3, corp-5;
   0 w aktach, bankowych i korpusie syntetycznym. Kontrakt 1.3.0.
4. **US4 — ramki** (R4): to schematy kroków (FR-067, `StepSequenceStage`), które w firm-2/4/5 psują strzałki-glify,
   treść poniżej pola i fałszywy „wiersz nagłówków” (który gubi punkt tekstu). Rozszerzenie etapu: łączniki-glify jako
   artefakt, przelew treści poniżej pola, nagłówek tylko w całości pogrubiony, osłona pola (niebiałe, wąskie,
   pogrubiony tekst, bez linii), pojedyncze pole.
5. **US5 — regresja**: punkt odniesienia `baseline-008/`, rozszerzone miary i porównanie zbiorów słów, pełny zestaw
   kontroli przed każdym commitem parsera; na końcu wersja 1.2.0 i manifest korpusu.

## Technical Context

**Language/Version**: C# 13 / .NET 9 (`net9.0`), SDK z `global.json` — bez zmian

**Primary Dependencies**: bez zmian (`PdfPig`, `Microsoft.Extensions.*`)

**Storage**: N/A (prywatny korpus właściciela w `tests/LegalAgent.PdfParser.Tests/Corpus/private`, poza git)

**Testing**: xUnit v3 na MTP; testy etapów (`LayoutFactory`, `PageSketch`, `StageHarness`), repliki stron
(`SyntheticPdfBuilder` z `src/LegalAgent.Corpus/Pdf/`, `TableSheet`), goldeny `Corpus/acts` i `Corpus/banking`,
prywatny korpus (`LEGALAGENT_PRIVATE_CORPUS`, `LayoutMetrics`), korpus syntetyczny (`refresh`, `verify`, `CorpusFull`)

**Target Platform**: Windows i Linux (CI ubuntu-latest); wynik deterministyczny

**Project Type**: biblioteka klas (`LegalAgent.PdfParser`) + CLI; aplikacje bez zmian kodu (ewentualnie
`FaqGrounding` — patrz niżej)

**Performance Goals**: bez regresji SC-007 (100 stron < 10 s); wszystkie nowe przebiegi liniowe względem liczby wierszy

**Constraints**: wynik wyłącznie z tekstu PDF (FR-640), jedynym usuwanym tekstem są stopki „N/M” i znaki strzałek;
reguły z geometrii i typografii (FR-641); goldeny `Corpus/acts`, `Corpus/banking`, korpus syntetyczny bez zmian bez
zgody (FR-644); prawdziwe PDF-y i nazwy banków poza git

**Scale/Scope**: zmiany w `ArtifactRemovalStage` (+ opcja `PageNumberBandRatio`), `ListDetectionStage`,
`HeadingDetectionStage`, nowy pomocnik „wiersz kontynuacji”, `ListLabelPatterns` + 2 wartości `ListLabelKind`,
`TableDetectionStage` (`IsHangingLabelBefore`), `TableDocumentStage` (wyłączenie nowych rodzajów),
`StepSequenceStage`; ~30 wymagań FR-600…FR-647, 8 kryteriów SC-090…SC-097

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Zasada / ograniczenie | Ocena | Jak spełnione |
|---|---|---|
| I. Test-First / TDD | ✅ | Każda poprawka: test etapu lub replika strony (red, sprawdzony na kodzie sprzed zmiany, osobny commit), potem kod (green). Testy offline; prawdziwe PDF-y tylko w prywatnym korpusie. |
| II. Wierność źródłu | ✅ | Etykiety dosłownie; usuwane tylko numery stron i glify strzałek (elementy graficzne); fałszywy nagłówek z dopisaną kropką znika; odzyskany tekst firm-2 wraca. Porównanie zbiorów słów (FR-645). |
| III. Powtarzalność | ✅ | Deterministyczne sortowania i progi; kultura niezmienna we wzorcach; miary jednym pomocnikiem. |
| IV. Odporność na błędy | ✅ | Brak wywołań zewnętrznych; obszar niespełniający warunków wraca do dotychczasowej ścieżki (ciąg „N/M” usuwany w całości albo wcale). |
| V. Bezpieczeństwo i konfiguracja | ✅ | Prywatne dokumenty, lista fraz ramek i `baseline-008/` poza git; repliki bez nazw banków. |
| VI. Prostota | ✅ | Rozszerzenia istniejących etapów (R1–R4), bez nowego etapu i adnotacji; jedna nowa opcja; zero nowych zależności. |
| VII. Dokumentacja | ✅ | README parsera (stopki „N/M”, „a.”/„i.”, schematy ze strzałkami, nowa opcja), CLAUDE.md (jeśli zmienia się opis etapów), handoff w planie. |
| Biblioteka ogólna | ✅ | Reguły opisują układ strony; brak nazw dokumentów i wydawców. |
| API jawne, SemVer | ✅ | Kontrakt 1.3.0 i wersja projektów 1.2.0 (MINOR: nowe wartości enum i opcja) — [contracts/markdown-output.md](./contracts/markdown-output.md). |

**Wynik bramki (przed Phase 0)**: PASS.

**Re-check po Phase 1**: PASS — zmiany modelu addytywne ([data-model.md](./data-model.md)); jedyne ryzyka (fałszywe
listy z „r.”, ramka pomylona z tabelą danych, wiersz kontynuacji zamiast prawdziwego „Art.”) mają osłony zmierzone na
całym korpusie w research i są pokryte FR-643/SC-094.

## Project Structure

### Documentation (this feature)

```text
specs/008-parser-fixes-after-007/
├── plan.md              # ten plik
├── research.md          # Phase 0: R1 „N/M”, R2 nagłówki, R3 „a.”/„i.”, R4 ramki, R5 wersje i kolejność
├── data-model.md        # Phase 1: ListLabelKind, PageNumberBandRatio, wiersz kontynuacji, ramka, miary
├── quickstart.md        # Phase 1: walidacja
├── contracts/
│   └── markdown-output.md   # uzupełnienie kontraktu Markdown (1.3.0)
├── checklists/requirements.md
└── tasks.md             # /speckit-tasks
```

### Source Code (repository root)

```text
src/LegalAgent.PdfParser/
├── Model/ListItem.cs                    # ListLabelKind: LetterDot, RomanDot
├── Options/PdfParserOptions.cs          # ArtifactOptions.PageNumberBandRatio (+ walidator)
├── Text/ListLabelPatterns.cs            # „a.”, „i.” (cały token + tekst, oba odczyty dla „i.”, „v.”, „x.”)
├── Text/ContinuationLine.cs             # nowy pomocnik „wiersz kontynuacji” (nazwa robocza; może trafić do Layout/)
├── Stages/ArtifactRemovalStage.cs       # przebieg samodzielnych „N/M” w pasie 15%
├── Stages/ListDetectionStage.cs         # bramka ciągów „a.”/„i.”, wiersz kontynuacji zamiast LegalUnit
├── Stages/HeadingDetectionStage.cs      # wiersz kontynuacji nie jest nagłówkiem jednostki
├── Stages/TableDetectionStage.cs        # IsHangingLabelBefore: nowe rodzaje
├── Stages/TableDocumentStage.cs         # HasListOrParagraphs: bez nowych rodzajów
└── Stages/StepSequenceStage.cs          # łączniki, przelew, nagłówek pogrubiony, osłona pola, pojedyncze pole

src/LegalAgent.Faq/FaqGrounding.cs       # tylko jeśli test pokaże, że cytat przez „a\.”/„ii\.” nie jest znajdowany

tests/LegalAgent.PdfParser.Tests/
├── Unit/Stages/ArtifactRemovalStageTests.cs, HeadingDetectionStageTests.cs, ListDetectionStageTests.cs,
│   StepSequenceStageTests.cs
├── Unit/Text/ListLabelPatternsTests.cs
├── Integration/ArtifactCleanupIntegrationTests.cs   # replika stopki „N/M”
├── Integration/CorpusLayoutsIntegrationTests.cs     # replika „Rozdział I” + ochrona „Art. 42.”
├── Integration/GlossaryLayoutTests.cs               # termin kończący się „/” (regresja), „a.” w definicji
├── Integration/HangingLabelLayoutTests.cs           # T014a/T014b rozszerzone o „a.”/„i.”
├── Integration/TablesIntegrationTests.cs            # „i.”/„ii.” przy tabeli (koniec TBL001)
├── Integration/StepSchemesIntegrationTests.cs       # replika ramek ze strzałkami-znakami
├── Corpus/PrivateCorpusTests.cs                     # nowe miary, porównanie zbiorów słów z baseline-008
└── Fixtures/LayoutMetrics.cs                        # nowe miary
```

**Structure Decision**: wyłącznie biblioteka parsera i jej testy; `LegalAgent.Chunking`, aplikacje i generator korpusu
bez zmian kodu (korpus tylko odświeżony: wersja w manifeście). `LegalAgent.Faq` tylko po czerwonym teście.

## Kolejność realizacji (wskazówka dla /speckit-tasks)

1. **Przygotowanie**: kopia goldenów prywatnych do `Corpus/private/baseline-008/`; `LayoutMetrics` + raport: nowe
   miary (akapity „N/M”, etykiety „a.”/„i.” poza listą, ramki jako tabela / wplecione z nieśledzonej listy fraz) i
   porównanie zbiorów słów z `baseline-008/`; pomiar wyjściowy zapisany w handoffie.
2. **US1**: testy etapu i replika (red) → przebieg „N/M” + opcja i walidator (green). Goldeny ind-2 i firm-1 (tylko
   usunięte stopki — sprawdzić diffem, że nic innego). FR-643.
3. **US2**: (b) test regresji słowniczka (od razu zielony — commit jako test, nie red/green); (a) replika + ochrona
   „Art. 42.” (red) → pomocnik i dwa miejsca użycia (green). Golden D-A. FR-643.
4. **US3**: testy wzorców (red) → wzorce i enum (green); repliki T014a/T014b, słowniczek, tabela (red) → bramka ciągu,
   `IsHangingLabelBefore`, `TableDocumentStage` (green); test `FaqGrounding` z cytatem przez `- a\.` (red tylko jeśli
   nie przechodzi). Goldeny corp-1, corp-3, corp-5. Kontrakt 1.3.0. FR-643.
5. **US4**: testy etapu (red) → łączniki, przelew, nagłówek, osłona, pojedyncze pole — każde osobno red/green; replika
   ramek (red → green). Goldeny firm-2, firm-4, firm-5; ind-1, ind-2, firm-3 bez zmian. FR-643.
6. **Zamknięcie**: `<Version>` 1.2.0, `refresh` + `verify` (manifest), README parsera, CLAUDE.md, tabela miar
   przed/po i zbiory słów w handoffie, testy wydajności.

## Stan prac i przekazanie

- 2026-10-10: spec (z clarify: zgoda na stopki ind-2 i firm-1, ramki-tabele GFM w zakresie, zasady zatwierdzania
  goldenów dla firm), research R1–R5 na prywatnych PDF-ach i całym korpusie, data-model, kontrakt Markdown 1.3.0,
  quickstart. Spec poprawiony po research: termin „15/ …/” już naprawiony (tylko test regresji), „a.”/„i.” w corp-1,
  corp-3, corp-5 (nie w firm-2), ramki także w firm-4 i firm-5, utracony punkt ramki w firm-2 (FR-634), łączniki
  (FR-635). Sondy w scratchpadzie sesji — nie są częścią repozytorium.
- Znane ograniczenie (poza zakresem): `HeadingDetectionStage` łączy oznaczenie i tytuł jednostki kropką
  (`"{Designation}. {Title}"`) także wtedy, gdy w PDF jej nie ma — zachowanie kontraktu 001, dotyczy wielu goldenów.
- 2026-10-10: tasks.md (T001–T046). Następny krok: `/speckit-implement` od T001 (Setup + Foundational + US1 = MVP).
- 2026-10-10 (T001–T005, punkt odniesienia i miary): goldeny 19 dokumentów skopiowane do nieśledzonego
  `Corpus/private/baseline-008/`; `frame-phrases.txt` (nieśledzony) z fraz lewych kolumn ramek odczytanych z PDF-ów
  (firm-2: 14 niepustych pól + 2 puste pominięte, firm-4: 3, firm-5: 28; zagnieżdżone prostokąty wiersza w firm-4
  scalone w jedną ramkę). Prywatny korpus zielony na starcie. Nowe miary w `Fixtures/LayoutMetrics.cs`
  (`PageNumberParagraphs`, `DotEnumLabelsOutsideList`, `UnitHeadingDiff`, `FramesAsTable`, `FramesWoven`,
  `WordSetDiff`; commity red 5e3d769 i green 6e087b1), raport w `PrivateCorpusTests` (nowe kolumny i sekcja „zbiory
  słów i nagłówki jednostek vs baseline-008”; bez plików prywatnych nie zawodzi).
  Wartości wyjściowe (tylko dokumenty z niezerową miarą nowych kolumn; po PR #10):

  | Dokument | Akapity „N/M” (wszystkie „N/M”) | „a.”/„i.” poza listą | Ramki jako tabela / wplecione |
  |---|---|---|---|
  | mbank-corp-1 | 0 | 20 | – |
  | mbank-corp-3 | 0 | 2 | – |
  | mbank-corp-5 | 0 | 9 | – |
  | mbank-firm-1 | 6 (6) | 0 | – |
  | mbank-firm-2 | 0 | 0 | 3 / 1 |
  | mbank-firm-4 | 0 | 0 | 2 / 0 |
  | mbank-ind-2 | 16 (35) | 0 | – |

  Zbiory słów i nagłówki jednostek vs baseline: zera we wszystkich 19 dokumentach (punkt odniesienia = bieżący stan).
  Odchylenia od oczekiwań z tasks.md: (1) w ind-2 tylko 16 z 35 „N/M” to osobne akapity — pozostałe 19 jest wklejone na
  końcu wierszy tekstu, listy lub przypisu (miara „N/M” = 35 jak dotąd; po US1 oba mają spaść do 0); (2) ramki firm-2
  jako tabela / wplecione: 3 / 1 zamiast 7 / 6 — fraza z PDF jest w goldenie często pocięta („**Sprawdzamy Twoją**”) lub
  sklejona z tekstem w jednym akapicie, więc miara oparta na całych frazach ich nie liczy; wartość służy jako
  punkt odniesienia, nie jako oczekiwane 7 / 6. Miary ramek zerujemy w T041 przez oczekiwany wynik „akapit frazy +
  lista”, a nie przez liczby z tej tabeli; (3) `UnitHeadingDiff` nie ma wartości wyjściowej (porównanie z samym sobą).
- 2026-10-10 (US1, T006–T012 oraz T010a, T010b — gotowe): stopki „N/M” nad strefą marginesu są usuwane jako numery
  stron (opcja `Artifacts.PageNumberBandRatio`, 0,15). Usunięcie stopek ujawniło dwa błędy, które wcześniej maskowały
  stopki, więc doszły T010a i T010b: (1) stopki przerywały listy na granicach stron — po ich usunięciu listy przechodzą
  przez strony, styl treści (mierzony bez elementów list) się odwracał i w ind-2 znikało 35 nagłówków (T010a: styl
  treści mierzony na tekście ciągłym łącznie z elementami list); (2) szerokość kolumny tekstu w firm-1 zależała od
  stopki (T010b: kolumna mierzona łącznie z elementami list). Commity: red 5a10e11, green 39d079e (T006–T010);
  red 79b66c7, green 4297d5b (T010a); red 75bf29b, green 349941e (T010b).
  Miary przed → po: „N/M” (stopki) ind-2 35 → 0, firm-1 6 → 0; akapity „N/M” ind-2 16 → 0, firm-1 6 → 0; pozostałe
  miary jak w raporcie `layout-metrics.md` (m.in. „Etykiety poza listą” firm-5 0 → 1 — artefakt miary: akapit spisu
  treści zaczyna się od `1\.`; „a.”/„i.” poza listą bez zmian: corp-1 20, corp-3 2, corp-5 9).
  Goldeny prywatne zmienione: ind-2 (stopki usunięte, listy ciągłe przez strony, tabela ze s. 8 → schemat kroków,
  przypis [^1] bez przyklejonych stopek), ind-3 (jeden nagłówek `## i… już dziecko może korzystać z karty` → `###`),
  firm-5 (nagłówek `## Spis treści`, przypisy podzielone na akapity, jeden nagłówek `##` → `###`) — zatwierdzone przez
  właściciela 2026-10-10; firm-1 (tylko stopki) i firm-2 (jeden poziom nagłówka, przejrzane przez wykonawcę).
  Korpus syntetyczny: REG-04 / ZAT-REG-SPR-02 (podpis → nagłówek; zatwierdzone przez właściciela). `generate` był
  potrzebny raz, bo migawka opcji w manifeście zyskała `pageNumberBandRatio`; po nim `verify` zielone.
  Zbiory słów vs baseline-008: utracone tylko tokeny „N/M” (34, 35) i powtórzony nagłówek ze s. 8 „Kolejność działań |
  Wyjaśnienie” w ind-2; nowych słów brak; nagłówki jednostek bez zmian. Prywatny korpus w pełni zielony (1090 testów
  parsera). Następny krok: US2 (T013).

## Complexity Tracking

Brak naruszeń konstytucji — sekcja pusta.
