# Tasks: Ostrzeżenia FTN001_OrphanFootnote po spec 010 (parser 1.3.1)

**Input**: Design documents from `specs/011-ftn001-false-warnings/` (plan, spec, research R1–R4, data-model,
contracts/conversion-report.md, quickstart)

**Tests**: wymagane: konstytucja I (TDD) i FR-803. Każda zmiana zachowania to osobny commit **red** (test pada na
kodzie 1.3.0; sprawdzić uruchomieniem) i potem commit **green** (kod). Testy działają offline. Repliki stron:
`PageSketch` (testy etapu) i `SyntheticPdfBuilder` (`src/LegalAgent.Corpus/Pdf/SyntheticPdfBuilder.cs`;
odnośnik = mniejszy `Text` na podniesionej linii bazowej, jak pomocniki `TrailingMarker`/`LeadingMarker` w
`tests/LegalAgent.PdfParser.Tests/Integration/BankTablesIntegrationTests.cs`). Repliki bez treści prywatnego aktu,
tylko układ: tekst 12 pt, odnośnik 8 pt podniesiony o ok. 4,5 pt, definicje 10 pt z etykietą 6,5 pt podniesioną o
ok. 4 pt, blok definicji na dole strony **bez kreski** (research R1, s. 67).

**Zasady commitów**: przed każdym commitem uruchom `git branch --show-current` jako osobne polecenie (nigdy
`main`). Stage'uj tylko jawne ścieżki (nigdy `git add -A`, bez `git stash -u`). Każdy commit kończy trailer
`Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`. Kod, komentarze i commity po angielsku; spec, handoff i
README po polsku.

**Goldeny** (FR-805): żaden `*.expected.md` ani golden chunków nie może się zmienić. Jeśli test goldena padnie,
zatrzymaj się i zgłoś diff właścicielowi; nie aktualizuj go.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: można równolegle (inne pliki, brak zależności)
- Etykiety historii: US1 (diagnoza), US2 (spójne ostrzeżenia FTN001), US3 (przypisy spec 010 bez zmian)

---

## Phase 1: Setup (punkt odniesienia)

- [X] T001 Pomiar wyjściowy na 1.3.0 (przed zmianą kodu): dla każdego PDF-u z
  `tests/LegalAgent.PdfParser.Tests/Corpus/acts/`, `tests/LegalAgent.PdfParser.Tests/Corpus/private/`,
  `publish/mBank.FaqGenerator-win-x64/downloads/` i dwóch prywatnych PDF-ów z katalogu głównego oraz dokumenty z
  `BankingCorpusGenerator.Documents()` (bez PDF-ów na dysku; pomiar tym samym testem diagnostycznym, który konwertuje
  ich bajty) zapisz liczbę `FTN001_OrphanFootnote` z raportu i SHA-256 Markdownu. Użyj CLI `convert … --report` do
  katalogu tymczasowego poza repozytorium. Dla każdego dokumentu zapisz też liczbę szkiców przypisów, których
  wszystkie powiązane odnośniki leżą w linii z rolą `Footnote` (samoodnośniki). Policz to tymczasowym testem
  diagnostycznym w `tests/LegalAgent.PdfParser.Tests/Scratch/`, pomijanym domyślnie i niecommitowanym, który
  uruchamia `FootnoteDetectionStage` i czyta `FootnoteDraft` oraz `LayoutWord.FootnoteId`. Oczekiwane na 1.3.0: 6 w
  `ustawa-o-sluzbie-cywilnej` i w D20081505Lj, wszędzie indziej 0. Tabelę (nazwa, FTN001, hash) wpisz do handoffu `specs/011-ftn001-false-warnings/plan.md` („Stan prac
  i przekazanie”). Oczekiwane FTN001 według research R2: akty 2/5/24/10/6/5, firm-1 2, firm-3 1, ind-2 1, ind-4 7,
  reg1 6, regulamin-pdp 6, pozostałe 0.
- [X] T002 Sprawdź, że pełny zestaw jest zielony na starcie: `dotnet test LegalAgent.slnx --filter "Category!=Performance"`
  oraz testy parsera z `LEGALAGENT_PRIVATE_CORPUS` (ścieżka bezwzględna do
  `tests/LegalAgent.PdfParser.Tests/Corpus/private`). Wynik zapisz w handoffie.

---

## Phase 2: Foundational

Brak: zmiana dotyczy jednego etapu, bez nowej infrastruktury.

---

## Phase 3: User Story 1 - Diagnoza każdego ostrzeżenia (Priority: P1)

**Goal**: przyczyna i klasyfikacja każdego ostrzeżenia w research.md (FR-800, FR-800a).

**Independent Test**: research.md R1 ma 5/5 ostrzeżeń aktu z przyczyną i klasyfikacją; R2 ma każde ostrzeżenie nowe
względem 1.2.3 przypisane do klasy K1–K3 (SC-120).

- [X] T003 [US1] Porównaj pomiar z T001 z tabelą R2 w `specs/011-ftn001-false-warnings/research.md`. Rozbieżności
  (inny dokument, inna liczba) dopisz do R2 z klasą przyczyny. Jeśli pojawi się klasa spoza K1–K3, zatrzymaj się i
  zgłoś ją właścicielowi przed US2.

**Checkpoint**: diagnoza kompletna; US2 może startować.

---

## Phase 4: User Story 2 - Spójne i wiarygodne ostrzeżenia FTN001 (Priority: P1) 🎯 MVP

**Goal**: podniesiona etykieta otwierająca linię definicji nigdy nie wiąże definicji, także bez kreski (FR-801);
liczba FTN001 aktów utrwalona w teście (FR-810); Markdown bez zmian (FR-807).

**Independent Test**: test etapu i replika strony bez kreski zgłaszają FTN001 dla definicji „3)”, do której
prowadzi tylko odnośnik z przecinkiem; bramka: `ustawa-o-sluzbie-cywilnej` 11, pozostałe akty jak w 1.3.0.

### Tests (red) for User Story 2

- [X] T004 [P] [US2] W `tests/LegalAgent.PdfParser.Tests/Unit/Stages/FootnoteDetectionStageTests.cs` dodaj test
  `RaisedDefinitionLabelsAtTheBottomWithoutRule_AreNoReferences_SoAnUnreferencedDefinitionIsOrphan`:
  - strona `BodyLines` bez kreski;
  - `WithReference(page, "…", "2)", 180)`: rozpoznany odnośnik na końcu słowa;
  - druga linia treści z odnośnikiem nierozpoznawanym: tekst `Body`, podniesione „3)” 6 pt, a za nim pełnowymiarowy
    „,” i dalszy tekst;
  - na dole strony dwie linie definicji przez `RaisedLabel(page, "2)", " …", 760, Note)` i
    `RaisedLabel(page, "3)", " …", 775, Note)`.

  Oczekiwane: dwa szkice o niezmienionym tekście; „2)” powiązany (`IsOrphan == false`, `FootnoteId` na słowie
  odnośnika w treści); „3)” `IsOrphan == true`; dokładnie jedno ostrzeżenie `FTN001_OrphanFootnote` ze stroną 1;
  słowo etykiety „3)” w linii definicji bez `FootnoteId`. Na 1.3.0 test MUSI paść (samoodnośnik wiąże „3)”).
- [X] T005 [P] [US2] Utwórz `tests/LegalAgent.PdfParser.Tests/Integration/FootnoteWarningsIntegrationTests.cs` z
  repliką strony aktu przez `SyntheticPdfBuilder`, z geometrią jak w nagłówku tego pliku:
  - artykuł z tekstem 12 pt;
  - odnośnik „8)” na końcu słowa przed spacją (rozpoznawany);
  - odnośniki „2)” i „3)” z przecinkiem za nimi (nierozpoznawane);
  - na dole strony, bez kreski, definicje „2) …”, „3) …”, „8) …” 10 pt z etykietą 6,5 pt podniesioną.

  Konwersja przez `PdfMarkdownConverter.CreateDefault()`. Oczekiwane:
  - raport: FTN001 dokładnie dla „2)” i „3)”;
  - Markdown: wynik 1.3.0 tej repliki (ustal go uruchomieniem na kodzie sprzed zmiany i zapisz jako literał w teście:
    definicje jako `[^n]:`, „8)” jako `[^n]`, „2)” i „3)” sklejone w tekście).

  Na 1.3.0 asercja raportu MUSI paść; asercja Markdownu przechodzi.
- [X] T006 [P] [US2] W `tests/LegalAgent.PdfParser.Tests/Corpus/GoldenTests.cs` dodaj teorię
  `Act_OrphanFootnoteWarnings_MatchExpectedCount(string name, int expected)` z `InlineData`:
  `dz-u-2020-287-prawa-konsumenta` 2, `dz-u-2024-1497-kredyt-konsumencki` 5, `dz-u-2024-1646-prawo-bankowe` 24,
  `dz-u-2024-30-uslugi-platnicze` 10, `dz-u-2026-1298-obwieszczenie-msz` 6, `ustawa-o-sluzbie-cywilnej` 11.
  Konwertuje PDF z `Corpus/acts` istniejącym `ConvertAsync` i liczy ostrzeżenia `FTN001_OrphanFootnote` w
  `result.Report.Warnings`. Dodaj test pilnujący, że każdy PDF z `Corpus/acts` ma wpis w tej teorii (FR-810: nowy
  akt wymaga świadomego dopisania liczby). Komentarz w kodzie: zmiana liczby = wpis w handoffie. Na 1.3.0 pada
  tylko `ustawa-o-sluzbie-cywilnej` (5).
- [X] T007 [US2] Uruchom T004–T006 na 1.3.0 i potwierdź oczekiwane niepowodzenia (T004, raport w T005,
  `ustawa-o-sluzbie-cywilnej` w T006); reszta zestawu zielona. Commit **red**:
  `test: raised definition labels never reference their own footnote, FTN001 counts of acts (red, spec 011 T004-T007)`
  ze ścieżkami trzech plików testów.

### Implementation (green) for User Story 2

- [X] T008 [US2] W `src/LegalAgent.PdfParser/Stages/FootnoteDetectionStage.cs`, w pętli wiązania w `Execute`
  (`foreach (IGrouping<LayoutLine, Marker> byLine in markers[page].GroupBy(m => m.Line))`), pomiń znaczniki
  etykiety definicji: `m.Line.Role == LineRole.Footnote && m.WordIndex == 0 && m.GlyphStart == 0` (data-model.md,
  „Reguła (FR-801)”). Wyodrębnij to do nazwanej metody pomocniczej, np. `IsDefinitionLabel(Marker)`, z komentarzem
  XML. Zbioru `referenced` i `FindMarkers` **nie** zmieniaj (research R4: inaczej obszar przypisów bez kreski
  znika i zmienia się Markdown). Zaktualizuj `<remarks>` klasy: etykieta otwierająca linię definicji nigdy nie jest
  odnośnikiem, z kreską i bez niej.
- [X] T009 [US2] Uruchom T004–T006 (zielone), pełny zestaw `dotnet test LegalAgent.slnx --filter "Category!=Performance"`
  (0 zmian goldenów, brak `*.actual.md`) i testy parsera z `LEGALAGENT_PRIVATE_CORPUS`. Commit **green**:
  `fix: a raised label opening a footnote line never links its own definition, also without a rule (green, spec 011 T008)`.

**Checkpoint**: US2 działa i jest przetestowana; Markdown wszystkich goldenów bez zmian.

---

## Phase 5: User Story 3 - Wykrywanie przypisów ze spec 010 bez zmian (Priority: P1)

**Goal**: brak regresji przypisów bankowych i liczb ostrzeżeń poza aktem o służbie cywilnej (FR-804, FR-806,
SC-123, SC-125).

**Independent Test**: istniejące testy przypisów spec 010 przechodzą bez zmian oczekiwań; prywatny korpus bez różnic.

- [X] T010 [US3] Pełny zestaw parsera z `LEGALAGENT_PRIVATE_CORPUS` (ścieżka bezwzględna). Oczekiwane: goldeny bez
  zmian, `RegulationNumbers_HaveNoExcess` przechodzi, testy `FootnoteDetectionStageTests` i
  `BankTablesIntegrationTests` bez zmian w kodzie testów. Wynik w handoffie.
- [X] T011 [US3] Powtórz pomiar z T001 na nowym kodzie i porównaj z nim. Oczekiwane:
  - hash Markdownu identyczny dla każdego dokumentu;
  - FTN001 identyczne (także `Corpus/banking`) poza `ustawa-o-sluzbie-cywilnej` i „Akt prawny - D20081505Lj.pdf”
    (5 → 11: s. 13, 67 ×6, 68, 70, 71 ×2);
  - samoodnośniki (pomiar z T001): 0 w każdym dokumencie (SC-121);
  - chunki aktu prywatnego (`chunk`) identyczne z `publish/porownanie-1.3.0/akt-D20081505Lj.new.chunks.jsonl` przy
    tych samych `--id`/`--designation` (ustal je z pliku porównawczego).

  Każdą inną różnicę zatrzymaj i zgłoś właścicielowi. Tabelę wyników wpisz do handoffu.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [X] T012 Wersja parsera 1.3.1 w `Directory.Build.props` (`<Version>1.3.1</Version>`); potem
  `dotnet run --project src/LegalAgent.Corpus.Cli -c Release -- refresh` i `… -- verify`. Oczekiwana zmiana tylko
  wersji w `corpus/manifest.json` (inną różnicę w `corpus/` zgłoś). Commit:
  `chore: parser version 1.3.1 and corpus manifest (spec 011 T012)` z `Directory.Build.props` i
  `corpus/manifest.json`.
- [X] T013 [P] README parsera `src/LegalAgent.PdfParser/README.md`:
  - sekcja wersji 1.3.1 (po „Wersja 1.3.0”): koniec samoodnośnika; ostrzeżeń FTN001 może być więcej niż w 1.2.3,
    bo dawniej etykieta definicji maskowała brak odnośnika;
  - w tabeli „Ostrzeżenia i raport” doprecyzuj FTN001 według `specs/011-ftn001-false-warnings/contracts/conversion-report.md`;
  - w „Znanych ograniczeniach” rozwiń punkt o przypisach o klasy z research R2: odnośnik z pełnowymiarową
    interpunkcją za nim („zm.1))”, „BLIK2,”), lista odnośników („BLIK9,10”), odnośnik w środku słowa
    („zł/2230.000”), odnośnik złożony do sąsiedniej linii, punkty listy wewnątrz przypisu jako osobne definicje.
    Bez nazw prywatnych dokumentów i banku.
- [X] T014 [P] `CLAUDE.md`: dopisek „Spec 011 additions (parser 1.3.1)” w sekcji architektury parsera (po spec 010),
  po angielsku:
  - a label opening a footnote line never links a definition (`LinkMarkers`), with or without a rule;
  - `referenced` still counts it for area detection;
  - FTN001 counts of acts are pinned in `GoldenTests`.
- [X] T015 Commit dokumentacji (T013, T014 i artefakty `specs/011-ftn001-false-warnings/`):
  `docs: FTN001 meaning, known footnote limitations, parser README 1.3.1 (spec 011 T013-T015)`.
- [ ] T016 Przebuduj `publish/` jak w spec 010 T041 (biblioteki `lib/`, pakiet NuGet 1.3.1, `LegalAgent.PdfParser/lib`,
  `LegalAgent.Chunking/lib`, exe generatora FAQ single-file win-x64; `appsettings*.json` bez zmian). Uruchom
  konwersję regulaminów z `publish/mBank.FaqGenerator-win-x64/{corp,firm,ind,downloads}` nowym CLI (0 błędów, FTN001
  jak w T011). Zapisz w handoffie. `publish/` jest poza gitem.
- [ ] T017 Handoff końcowy w `specs/011-ftn001-false-warnings/plan.md` („Stan prac i przekazanie”: pomiary T001/T011,
  commity, decyzje, otwarte sprawy: rozpoznawanie odnośników z interpunkcją jako kandydat na osobny spec) i `[X]` w
  tym pliku. Commit `docs: spec 011 handoff (T017)`. Push gałęzi `011-ftn001-false-warnings` po potwierdzeniu
  właściciela; PR tworzy właściciel.

---

## Dependencies & Execution Order

- **Setup (T001–T002)** → **US1 (T003)** → **US2 red (T004–T007)** → **US2 green (T008–T009)** → **US3 (T010–T011)**
  → **Polish (T012–T017)**.
- T001 musi powstać na kodzie 1.3.0 (przed T008); jest punktem odniesienia dla T011.
- T005 wymaga uruchomienia na 1.3.0, żeby ustalić literał Markdownu.
- T012 (wersja) po T011, żeby porównania nie mieszały zmiany kodu z wersją w manifeście.

### Parallel Opportunities

- T004, T005, T006: różne pliki testów, piszą się równolegle; commit red wspólny (T007).
- T013 i T014 równolegle (README i CLAUDE.md).

### Parallel Example: User Story 2

```text
T004 FootnoteDetectionStageTests.cs (test etapu)
T005 FootnoteWarningsIntegrationTests.cs (replika SyntheticPdfBuilder)
T006 GoldenTests.cs (bramka FTN001)
→ T007 commit red → T008 kod → T009 commit green
```

## Implementation Strategy

- **MVP**: US2 (T004–T009). Spójne FTN001 i bramka; Markdown bez zmian. US1 to dokumentacja już zrobiona w planie
  (T003 tylko ją potwierdza).
- **Przyrostowo**: po green US3 jako weryfikacja (bez kodu), potem wydanie 1.3.1 (T012–T016) i handoff.
- **Stop-punkty dla właściciela**: każda zmiana goldenu, każda różnica Markdownu w T011, klasa przyczyny spoza K1–K3
  w T003, push.
