---

description: "Lista zadań spec 012: oryginalne etykiety przypisów w Markdownie (parser 1.4.0)"
---

# Tasks: Oryginalne etykiety przypisów w Markdownie (parser 1.4.0)

**Input**: `specs/012-footnote-original-labels/` — plan.md, spec.md, research.md (R1–R6), data-model.md,
contracts/markdown-output.md (kontrakt 2.0.0), quickstart.md

**Tests**: wymagane (konstytucja I, FR-911): osobny commit red, potem osobny commit green. Goldeny parsera i chunków
zmieniają się dopiero po akceptacji właściciela (FR-163, FR-908) — osobnym commitem.

**Zasady wspólne dla wszystkich zadań z commitem**: przed commitem `git branch --show-current` osobnym poleceniem
(oczekiwane `012-footnote-original-labels`, nigdy `main`); stage'ować tylko jawne ścieżki (nigdy `git add -A`, bez
prywatnych plików z katalogu głównego i `src/LegalAgent.PdfParser.Cli/Properties/`); trailer
`Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`; push dopiero po potwierdzeniu właściciela.

## Format: `[ID] [P?] [Story] Opis`

- **[P]**: można równolegle (inne pliki, brak zależności od nieukończonych zadań)
- **[Story]**: US1 (etykiety z PDF-u), US2 (kolejność definicji), US3 (chunki)

---

## Phase 1: Setup (punkt odniesienia 1.3.1)

**Cel**: zapis stanu 1.3.1, z którym porównuje się SC-131 i SC-133, i skrypt normalizacji identyfikatorów.

- [X] T001 Pomiar 1.3.1 (przed jakąkolwiek zmianą kodu, gałąź na `5480789` lub późniejszym commicie bez zmian w
  `src/`): skonwertować CLI (`convert … --report …`, Release) wszystkie dokumenty z przypisami — `tests/LegalAgent.PdfParser.Tests/Corpus/acts/*.pdf`,
  `tests/LegalAgent.PdfParser.Tests/Corpus/banking/*.pdf`, prywatny korpus `tests/LegalAgent.PdfParser.Tests/Corpus/private/*.pdf`,
  `publish/mBank.FaqGenerator-win-x64/downloads/*.pdf` i dwa prywatne PDF-y z katalogu głównego — oraz `chunk` dla
  tych samych plików, do katalogu scratchpada `baseline-012/` (poza repozytorium). Dla każdego dokumentu zapisać
  w `baseline-012/counts.csv`: liczbę odnośników `[^…]` (bez `:`), liczbę definicji `[^…]:`, liczbę `FTN001_OrphanFootnote`
  z raportu.
- [X] T002 Skrypt porównania w scratchpadzie (`compare-footnotes.py` lub `.ps1`, nie w repozytorium), wywoływany jako
  `compare <stary.md> <nowy.md>`: (a) zamienia każdy `[^X]` i `[^X]:` na `[^#]` w obu plikach i wypisuje diff (SC-133:
  ma być pusty); (b) paruje identyfikatory w kolejności wystąpień i sprawdza, że odwzorowanie stary numer → nowy
  identyfikator jest bijekcją w obrębie dokumentu (ten sam przypis zawsze ma ten sam identyfikator); (c) wypisuje
  liczby odnośników/definicji obu plików (SC-131) oraz identyfikatory, które nie są etykietą „czystą”: z dopiskiem
  `-k` albo zastępcze (SC-134). Ten sam skrypt obsługuje `*.chunks.jsonl` (pole z Markdownem fragmentu, linia po linii).
  Sprawdzić skrypt na parze identycznych plików (pusty diff, odwzorowanie tożsamościowe).

---

## Phase 2: Foundational (szkielet modelu, bez zmiany zachowania)

**Cel**: testy red muszą się kompilować, więc nowe właściwości modelu powstają przed nimi, puste i nieużywane.

- [X] T003 Dodać do `src/LegalAgent.PdfParser/Model/Footnote.cs` właściwość `public string Identifier { get; init; } = "";`
  w ciele rekordu (rekord pozycyjny zostaje, konstruktor bez zmian), z komentarzem XML: identyfikator Markdownu
  przypisu (`[^Identifier]`), etykieta z PDF-u albo etykieta z dopiskiem `-k` albo numer zastępczy; pusty = renderer
  używa `Number`. To samo w `src/LegalAgent.PdfParser/Model/Inline.cs` dla `FootnoteRef` („identyfikator przypisu, na
  który wskazuje odnośnik”). Bez zmian w etapach i rendererze. `dotnet build LegalAgent.slnx -c Release` bez
  ostrzeżeń; sprawdzić, czy jakiś test porównuje serializację JSON modelu (`JsonDerivedType` w `Inline.cs`) — nowe
  pole nie może zmienić istniejącego snapshotu bez odnotowania w handoffie. Commit razem z T004–T006a (red).
  **Uzasadnienie odstępstwa od konstytucji I** (kod produkcyjny przed testem): bez tych właściwości testy red się nie
  kompilują; zadanie nie zmienia zachowania (właściwości puste i nieczytane) i trafia do tego samego commitu co
  testy, które je wymagają.

**Checkpoint**: rozwiązanie się buduje, wszystkie istniejące testy przechodzą jak w 1.3.1.

---

## Phase 3: User Story 1 — Przypis w Markdownie ma etykietę z PDF-u (Priority: P1) 🎯 MVP

**Goal**: odnośniki i definicje przypisów w Markdownie mają identyfikatory z etykiet PDF-u (FR-900–FR-904).

**Independent Test**: replika regulaminu (tabela „TAK²¹”…„TAK²⁴”, akapit z „⁷”) daje `TAK[^21]`…`TAK[^24]`, `[^7]` i
definicje `[^7]:`, `[^21]:`…`[^24]:`, żadnego innego identyfikatora.

### Tests for User Story 1 (red)

- [X] T004 [P] [US1] Testy reguły identyfikatora w `tests/LegalAgent.PdfParser.Tests/Unit/Stages/DocumentBuildStageTests.cs`
  (model zbudowany na `PipelineContext` jak w istniejących testach tej klasy), każdy osobną metodą `[Fact]`:
  (1) etykiety „21”, „22” → `Footnote.Identifier` „21”, „22” i `FootnoteRef.Identifier` odnośników takie same;
  (2) etykieta „1)” dwa razy (strony 1 i 3) → „1)” dla definicji ze strony 1, „1)-2” dla strony 3, a każdy odnośnik
  ma identyfikator definicji, z którą jest powiązany (nie pierwszej o tej etykiecie); (3) dwie definicje „1)” na
  tej samej stronie → kolejność po id szkicu; (4) kolizja dopisku: etykiety „1)”, „1)”, „1)-2” → „1)”, „1)-3”, „1)-2”
  („Dopisek nie może dać identyfikatora równego innej etykiecie dokumentu”, FR-903); (5) etykieta pusta → identyfikator
  = `Number` jako tekst i ostrzeżenie `FTN002_FootnoteWithoutLabel` ze stroną przypisu; (6) etykieta ze spacją,
  tabulatorem, `[` albo `]` → jak (5) („pusta, zawierająca spację, tabulator lub `[`/`]`”, kontrakt 2.0.0);
  (7) zastępczy numer zderza się z etykietą (pusta etykieta przypisu nr 2 i inny przypis z etykietą „2”) →
  zastępczy dostaje kolejny wolny numer; (8) odnośniki w tabeli — wiersz, nagłówek, grupa nagłówka i komórka
  kontynuacji (`ContinuesAbove`, współdzielone inline'y) — mają `Identifier`; (9) `Footnote.Number` i kolejność
  definicji bez zmian względem 1.3.1; (10) brak `FTN002` gdy wszystkie przypisy mają etykiety; (11) dwa odnośniki
  (akapit i komórka tabeli) do jednego przypisu „21” → oba mają `Identifier` „21”, jedna definicja; (12) etykiety
  „1)” i „1” w jednym dokumencie (spec 011 K3) → identyfikatory „1)” i „1”, bez dopisku.
- [X] T005 [P] [US1] Testy renderera w nowym pliku `tests/LegalAgent.PdfParser.Tests/Rendering/MarkdownRendererFootnoteTests.cs`
  (model zbudowany ręcznie): `Identifier` „21” → `TAK[^21]` i `[^21]: …`; „*)” → `ustawy[^*)]` i `[^*)]: …` bez
  ucieczki i bez wyróżnienia; „1)-2” → `[^1)-2]`; `Identifier` pusty (klient biblioteki 1.x) → `[^{Number}]` w
  odnośniku i definicji; odnośnik w komórce tabeli GFM → `[^21]`.
- [X] T006 [P] [US1] Repliki w nowym pliku `tests/LegalAgent.PdfParser.Tests/Integration/FootnoteLabelsIntegrationTests.cs`
  (`SyntheticPdfBuilder`/`TableSheet` z `Fixtures/`, wzorem `FootnoteWarningsIntegrationTests.cs`; treść wymyślona,
  bez tekstu dokumentów prywatnych, bez nazw prawdziwych banków): (a) regulamin — akapit z odnośnikiem „⁷”, tabela z
  rulingami z „TAK²¹”…„TAK²⁴”, kreska i definicje „7”, „21”…„24” w stopce → Markdown zawiera dokładnie identyfikatory
  7, 21, 22, 23, 24 (zbiór odnośników = zbiór definicji), żadnego `[^1]`; (b) akt z dwiema seriami „¹⁾”, „²⁾” na
  stronach 1 i 2 → `[^1)]`, `[^2)]`, `[^1)-2]`, `[^2)-2]`, odnośniki z każdej strony do swojej serii; (c) przypis
  „*)” → `[^*)]`; (d) w każdym przypadku liczba `FTN001_OrphanFootnote` jak przed zmianą (asercja liczby) i 0 `FTN002`.
- [X] T006a [P] [US1] Niezmiennik SC-130 w `tests/LegalAgent.PdfParser.Tests/Corpus/GoldenTests.cs`, uruchamiany na
  wszystkich dokumentach goldenów (akty, `banking`, prywatny korpus z `LEGALAGENT_PRIVATE_CORPUS`): dla każdego
  `Footnote` dokumentu (sekcje i preambuła) `Identifier` == `OriginalLabel`, albo `OriginalLabel + "-k"` (k ≥ 2,
  tylko gdy etykieta powtarza się w dokumencie), albo — tylko dla etykiety pustej lub ze spacją, tabulatorem, `[`,
  `]` — numer zastępczy z ostrzeżeniem `FTN002_FootnoteWithoutLabel` na stronie przypisu; identyfikatory unikalne
  w dokumencie; każdy `FootnoteRef.Identifier` ma definicję.
- [X] T007 [US1] Uruchomić `dotnet test tests/LegalAgent.PdfParser.Tests -- --filter-class "*DocumentBuildStageTests"`,
  `--filter-class "*MarkdownRendererFootnoteTests"`, `--filter-class "*FootnoteLabelsIntegrationTests"`,
  `--filter-method "*FootnoteIdentifiers*"` (T006a): nowe testy
  padają z właściwego powodu (identyfikator pusty / `[^1]` zamiast `[^21]`), stare przechodzą; test (10) z T004 i
  „pusty `Identifier` → `Number`” z T005 mogą przechodzić od razu — odnotować. Commit red (T003–T006a):
  `test: footnote identifiers from PDF labels (red, spec 012 T003-T006a)`.

### Implementation for User Story 1 (green)

- [X] T008 [US1] `src/LegalAgent.PdfParser/Stages/DocumentBuildStage.cs`: po `NumberFootnotes` (linia ~71) nowa
  funkcja `AssignIdentifiers` zwracająca mapę id szkicu → identyfikator według reguły z `data-model.md`: kandydaci
  w kolejności (`Page`, id szkicu); etykieta pusta lub ze spacją/tabulatorem/`[`/`]` → zastępczy `Number`
  (przy kolizji z etykietą kolejny wolny numer) i `context.Report.AddWarning("FTN002_FootnoteWithoutLabel", page,
  $"Przypis bez etykiety na stronie {page} dostał identyfikator zastępczy „{id}”.")`; pierwsze wystąpienie etykiety
  → etykieta; k-te → `etykieta-k`, k rośnie, dopóki identyfikator jest zajęty (etykiety dokumentu i już nadane
  identyfikatory). Najpierw zebrać zbiór wszystkich etykiet, potem nadawać. `ToFootnote` (linia ~130) ustawia
  `Identifier`; `Renumber(IReadOnlyList<Inline>, …)` (linia ~206–208) tworzy `new FootnoteRef(n) { Identifier = … }`
  — dotyczy też tabel (`Renumber(TableBlock, …)`, grupa nagłówka, nagłówek, wiersze, współdzielone inline'y komórek
  kontynuacji) i przypisów preambuły. Etykieta „²¹” jest już w `OriginalLabel` jako „21” (sprawdzić w teście (1)
  T004; jeśli nie, zamiana cyfr górnych w tej funkcji).
- [X] T009 [US1] `src/LegalAgent.PdfParser/Rendering/MarkdownRenderer.cs`: `RenderFootnotes` (linia ~73) i odnośnik
  (linia ~303) piszą `Identifier`, a gdy pusty — `Number`/`FootnoteNumber` (jedna prywatna funkcja dla obu miejsc).
  Bez ucieczek znaków w identyfikatorze (research R3).
- [X] T010 [US1] `tests/LegalAgent.PdfParser.Tests/Fixtures/NumberCheck.cs`: wzorzec odnośnika sklejonego
  (`\[\^(\d+)\](?!:)`, linia ~400) i `FootnoteLabels` (linia ~164, dziś po `Footnote.Number`) przestawić na
  identyfikatory (`[^1)-2]`, `[^*)]`, `[^21]`): cyfry etykiety i dopisku `-k` nie liczą się jako nadmiar (`Liczby +`)
  ani brak (`Liczby −`). `FootnoteDefinition`/`FootnoteReference` (`\[\^[^\]]+\]`) już obejmują nowe postacie —
  potwierdzić testem w `tests/LegalAgent.PdfParser.Tests/Unit/…/NumberCheckTests.cs` (jeśli istnieje; inaczej
  asercja w T006). `number-check.txt` bez zmian.
- [X] T011 [US1] Kod ostrzeżenia `FTN002_FootnoteWithoutLabel`: sprawdzić, czy kody są gdzieś wyliczone (stałe,
  testy `Unit/Pipeline/ConversionReportTests.cs`, kontrakt raportu w `specs/001-legal-pdf-parser/contracts/`) i dopisać
  tam nowy kod; kod wyjścia CLI bez zmian (konstytucja IV).
- [X] T012 [US1] Testy T004–T006a zielone; `dotnet test LegalAgent.slnx --filter "Category!=Performance"`: padają
  tylko goldeny parsera (`*.expected.md` → `*.actual.md`) i goldeny chunków (`*.actual.jsonl`), nic innego; bramka
  FTN001 `GoldenTests.Act_OrphanFootnoteWarnings_MatchExpectedCount` zielona (FR-909). Commit green tylko z kodem
  (`src/…`, `NumberCheck.cs`), bez goldenów: `feat: footnote identifiers are the PDF labels (green, spec 012 T008-T011)`.

### Raport goldenów i akceptacja (FR-908, FR-163)

- [ ] T013 [US1] Raport różnic skryptem z T002 dla: każdej pary `*.expected.md`/`*.actual.md` parsera, goldenów
  chunków `tests/LegalAgent.Chunking.Tests/Golden/*.chunks.jsonl`/`*.actual.jsonl`, prywatnego korpusu (z
  `LEGALAGENT_PRIVATE_CORPUS` ustawionym na ścieżkę bezwzględną) oraz plików z `baseline-012/` przekonwertowanych
  ponownie (downloads, katalog główny). Oczekiwane: diff po normalizacji pusty wszędzie (SC-133); liczby odnośników,
  definicji i FTN001 równe `baseline-012/counts.csv` (SC-131); dopiski `-k` tylko w prawo-bankowe i
  obwieszczenie-msz, identyfikator zastępczy i `FTN002` tylko w `Corpus/banking/regulamin-rachunku` (SC-134);
  w regulaminie kredytu gotówkowego z `downloads` tabela s. 21 ma odnośniki 21–24 (quickstart 4). Każde odstępstwo:
  zatrzymać, opisać i naprawić przed pokazaniem właścicielowi (nowe zadanie T013x).
- [ ] T014 [US1] **STOP — akceptacja właściciela.** Przedstawić raport T013 po polsku: lista zmienionych goldenów
  (parser publiczny, prywatny, chunki), przykładowe fragmenty przed/po (akt z dopiskiem, regulamin z tabelą,
  dokument z `FTN002`), wynik normalizacji. Nie uruchamiać `UPDATE_GOLDEN` bez wyraźnej zgody.
- [ ] T015 [US1] Po zgodzie: `UPDATE_GOLDEN=1 dotnet test tests/LegalAgent.PdfParser.Tests` (także z
  `LEGALAGENT_PRIVATE_CORPUS` — prywatne goldeny poza gitem) i `UPDATE_GOLDEN=1 dotnet test tests/LegalAgent.Chunking.Tests`;
  usunąć pozostałe `*.actual.*`; pełny zestaw zielony. Commit tylko śledzonych goldenów (`git status` → jawne
  ścieżki `tests/LegalAgent.PdfParser.Tests/Corpus/**/*.expected.md`, `tests/LegalAgent.Chunking.Tests/Golden/*.chunks.jsonl`):
  `test: goldens with PDF footnote labels, accepted by the owner (spec 012 T015)`.

**Checkpoint**: US1 kompletna — Markdown i chunki z etykietami PDF-u, goldeny zaakceptowane.

---

## Phase 4: User Story 2 — Przypisy w kolejności dokumentu (Priority: P1)

**Goal**: definicje sekcji w kolejności pierwszego odnośnika (akapity, listy, tabele w kolejności czytania), sieroty
po nich w kolejności stron (FR-905, FR-906). Research R2: kod 1.3.1 już to spełnia — test utrwala regułę.

**Independent Test**: replika z odnośnikiem „⁷” w akapicie nad tabelą z „²¹”, „²²” i sierotą „25” → definicje w
kolejności 7, 21, 22, 25.

- [ ] T016 [US2] W `tests/LegalAgent.PdfParser.Tests/Integration/FootnoteLabelsIntegrationTests.cs` test kolejności:
  akapit z „⁷” przed tabelą z „TAK²¹”, „TAK²²”, przypis „25” bez odnośnika na tej samej stronie i przypis-sierota
  „26” na następnej → definicje w Markdownie w kolejności `[^7]:`, `[^21]:`, `[^22]:`, `[^25]:`, `[^26]:`, w tej
  samej sekcji co w 1.3.1 (najgłębsza sekcja otwarta na stronie). Uruchomić: oczekiwane zielone od razu → commit
  `test: footnote definitions follow reading order (spec 012 T016)`. Jeśli pada: commit red, poprawka w
  `NumberFootnotes` (`DocumentBuildStage.cs`) jako osobny commit green (T016a/T016b) i ponowny raport goldenów (T013)
  przed jakąkolwiek zmianą goldenów.
- [ ] T017 [US2] Sprawdzić SC-132 na wszystkich dokumentach z T013 (skrypt z T002 albo jednorazowy w scratchpadzie):
  w każdej sekcji definicje stoją w kolejności pierwszego odnośnika, sieroty po nich w kolejności stron; wynik do
  handoffu.

**Checkpoint**: US1 i US2 działają niezależnie.

---

## Phase 5: User Story 3 — Te same etykiety w chunkach (Priority: P2)

**Goal**: chunki mają te same identyfikatory co Markdown dokumentu (FR-907); chunker bez zmian kodu (renderer wspólny).

**Independent Test**: unit podzielony na części; część z odnośnikiem zawiera `[^21]` i `[^21]:`.

- [ ] T018 [P] [US3] `tests/LegalAgent.Chunking.Tests/Fixtures/Doc.cs`: przeciążenia `Ref(int number, string identifier)`
  i `Footnote(int number, string text, int page = 1, string identifier = "")` ustawiające `Identifier` (istniejące
  wywołania bez zmian).
- [ ] T019 [US3] Test w `tests/LegalAgent.Chunking.Tests/Unit/FootnoteSelectionTests.cs` (lub `FragmentRendererTests.cs`,
  gdzie dziś są testy części z przypisami): unit dłuższy niż `MaxChunkLength` z odnośnikiem do przypisu o
  identyfikatorze „21” w drugiej części → ta część zawiera `[^21]` i `[^21]:`, pierwsza nie zawiera żadnej
  definicji; przypis o identyfikatorze „1)-2” → `[^1)-2]`. Wybór definicji po `Number` bez zmian
  (`src/LegalAgent.Chunking/Splitting/FootnoteSelector.cs` nie wymaga zmian — potwierdzić). Test jest zielony od razu
  (renderer z T009) — test regresji, odnotować w handoffie. Commit:
  `test: chunk parts keep PDF footnote labels (spec 012 T018-T019)`.

**Checkpoint**: wszystkie trzy historie działają.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [ ] T020 Podgląd GFM (research R3): plik w scratchpadzie z `[^21]`, `[^1)]`, `[^*)]`, `[^**)]`, `[^1)-2]` w
  akapicie i w komórce tabeli; sprawdzić w podglądzie Markdownu VS Code, że każdy odnośnik prowadzi do swojej
  definicji, a `*` nie daje kursywy. Podgląd na GitHubie (gist/PR) to publikacja na zewnątrz — przygotować fragment
  i poprosić właściciela o sprawdzenie w opisie PR; wynik do handoffu.
- [ ] T021 Wersja parsera 1.4.0 w `Directory.Build.props` (właściwość wersji pakietu `LegalAgent.PdfParser`, jak w
  spec 011 T012); `dotnet run --project src/LegalAgent.Corpus.Cli -c Release -- generate` (nie samo `refresh`:
  manifest ma `generatorVersion`), potem `verify`. W `git diff corpus/` zmieniają się tylko identyfikatory `[^…]` w
  `*.md`/`*.chunks.jsonl` i wersje w `corpus/manifest.json` — sprawdzić skryptem z T002. Commit
  `chore: parser version 1.4.0 and corpus (spec 012 T021)`.
- [ ] T022 [P] `src/LegalAgent.PdfParser/README.md`: sekcja „Wersja 1.4.0: etykiety przypisów z PDF-u (spec 012)” —
  kontrakt Markdown 2.0.0 (zmiana niezgodna: identyfikator to etykieta, nie numer ciągły), dopisek `-k` dla
  powtórzeń z przykładem `[^1)]`/`[^1)-2]`, identyfikator zastępczy; wiersz `FTN002_FootnoteWithoutLabel` w tabeli
  ostrzeżeń (obok `FTN001`, linia ~255); nowe właściwości `Footnote.Identifier`, `FootnoteRef.Identifier`
  (zgodność: pusty → `Number`); uwaga, że podgląd GFM może pokazywać własną numerację.
- [ ] T023 [P] Kontrakt: w `specs/001-legal-pdf-parser/contracts/markdown-output.md` przy regule numeracji przypisów
  odsyłacz „zastąpione w 2.0.0 — `specs/012-footnote-original-labels/contracts/markdown-output.md`”; nie zmieniać
  treści pozostałych reguł.
- [ ] T024 [P] `CLAUDE.md`: w „Rendering” `footnotes [^n]` → identyfikatory z etykiet PDF-u (kontrakt 2.0.0);
  sekcja „Spec 012 additions (parser 1.4.0)”: `DocumentBuildStage.AssignIdentifiers`, reguła dopisku i
  zastępczego, FTN002, `Identifier` w modelu, chunker bez zmian, `NumberCheck` po identyfikatorach.
- [ ] T025 Pełna weryfikacja: `dotnet test LegalAgent.slnx --filter "Category!=Performance"` (0 niepowodzeń, podać
  liczbę testów); prywatny korpus z `LEGALAGENT_PRIVATE_CORPUS` (bezwzględna ścieżka) zielony, w tym
  `RegulationNumbers_HaveNoExcess` bez zmian `number-check.txt`; FTN001 aktów 2, 5, 24, 10, 6, 11 (bramka spec 011);
  dokument z przykładu (quickstart 4) w scratchpadzie. Commit dokumentacji T022–T024:
  `docs: footnote labels, contract 2.0.0, parser README 1.4.0 (spec 012 T022-T024)`.
- [ ] T026 `publish/` jak w spec 011 T016: NuGet 1.4.0 tylko lokalnie w `publish/nuget/`, exe CLI self-contained,
  single-file, `EnableCompressionInSingleFile`; aplikacja FAQ w `publish/mBank.FaqGenerator-win-x64/` bez ruszania
  `downloads/` właściciela. Usunąć `*.actual.*` i pliki tymczasowe z repozytorium (do scratchpada).
- [ ] T027 Handoff: `specs/012-footnote-original-labels/plan.md` „Stan prac i przekazanie” (wyniki T013, T017, T020,
  T025, lista zmienionych goldenów, liczba testów), wszystkie zadania w tym pliku oznaczone `[X]`; pamięć projektu.
  Commit `docs: spec 012 handoff (T027)`. Push i opis PR dopiero po potwierdzeniu właściciela; potem rebase 013 na 012.

---

## Dependencies & Execution Order

- **Setup (T001–T002)**: przed jakąkolwiek zmianą `src/` — T001 mierzy 1.3.1.
- **Foundational (T003)**: przed testami red (kompilacja).
- **US1 (T004–T015)**: red (T004–T007, z T006a) → green (T008–T012) → raport (T013) → **STOP T014** → goldeny (T015).
- **US2 (T016–T017)**: po T015 (goldeny zaakceptowane; ewentualna poprawka kolejności zmienia goldeny ponownie).
- **US3 (T018–T019)**: po T009 (renderer); goldeny chunków aktualizowane razem z parserowymi w T015.
- **Polish (T020–T027)**: po US1–US3; T021 przed T025 (wersja w manifeście), T026 po T025.

### Parallel Opportunities

- T004, T005, T006, T006a — cztery różne pliki testów (red).
- T018 równolegle z T016 (inne projekty testowe).
- T022, T023, T024 — różne pliki dokumentacji.

## Parallel Example: User Story 1 (red)

```text
Task: "T004 reguła identyfikatora w Unit/Stages/DocumentBuildStageTests.cs"
Task: "T005 renderer w Rendering/MarkdownRendererFootnoteTests.cs"
Task: "T006 repliki w Integration/FootnoteLabelsIntegrationTests.cs"
Task: "T006a niezmiennik SC-130 w Corpus/GoldenTests.cs"
```

## Implementation Strategy

- **MVP**: Setup + T003 + US1 do T015 — Markdown i chunki z etykietami PDF-u, goldeny zaakceptowane.
- Potem US2 (utrwalenie kolejności, oczekiwane bez zmian kodu), US3 (test regresji chunków), Polish (wersja, korpus,
  dokumentacja, publish).
- Proste zadania (T001 konwersje, T018, T022–T024, T026) można zlecić Sonnetowi; T008, T010, T013 wymagają kontekstu
  reguły i wykonuje je główny agent.
