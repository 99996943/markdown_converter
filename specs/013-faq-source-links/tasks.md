---

description: "Lista zadań spec 013: linki z FAQ do miejsca źródła (strona PDF-u i nagłówek Markdownu)"
---

# Tasks: Linki z FAQ do miejsca źródła (strona PDF-u i nagłówek Markdownu)

**Input**: `specs/013-faq-source-links/` — plan.md, spec.md, research.md (R1–R7), data-model.md,
contracts/faq-file-links.md, quickstart.md

**Tests**: wymagane (konstytucja I, FR-1007): osobny commit red, potem osobny commit green; offline (fałszywy model,
syntetyczne regulaminy, bez sieci i Azure).

**Zasady wspólne dla zadań z commitem**: przed commitem `git branch --show-current` osobnym poleceniem (oczekiwane
`013-faq-source-links`, nigdy `main`); stage'ować tylko jawne ścieżki (nigdy `git add -A`, bez prywatnych plików z
katalogu głównego i `src/LegalAgent.PdfParser.Cli/Properties/`); trailer
`Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`; push dopiero po potwierdzeniu właściciela.
`faq/FAQ_mBank.md` w repozytorium zmienia się tylko po prawdziwym przebiegu właściciela (T024), nigdy z testów.

## Format: `[ID] [P?] [Story] Opis`

- **[P]**: można równolegle (inne pliki, brak zależności od nieukończonych zadań)
- **[Story]**: US1 (link do strony PDF-u), US2 (link do nagłówka Markdownu)

---

## Phase 1: Setup

- [X] T001 Punkt odniesienia SC-143: golden renderera `faq.expected.md` w `tests/LegalAgent.Faq.Tests` (ścieżka z
  `GoldenFile.PathOf`) — dla dokumentów bez położeń MUST zostać bez zmian w całej spec. Skopiować go do scratchpadu
  (poza repozytorium) jako `faq-1.4.0.md` i zanotować liczbę testów `tests/LegalAgent.Faq.Tests` i
  `tests/mBank.FaqGenerator.Tests` przed zmianami. Bez zmian w kodzie.

---

## Phase 2: Foundational (szkielet modelu, bez zmiany zachowania)

**Cel**: testy red muszą się kompilować.

- [X] T002 Nowy rekord `src/LegalAgent.Faq/Model/FaqUnitLocation.cs`: `public sealed record FaqUnitLocation(string Unit,
  int? Page, string? Anchor)` z komentarzami XML z data-model.md („`Page`: strona PDF-u, na której zaczyna się sekcja
  jednostki; `null` gdy nieznana”, „`Anchor`: kotwica nagłówka sekcji w Markdownie dokumentu (bez `#`); `null` gdy nie
  da się jej wyliczyć”). Właściwości `init` z wartościami domyślnymi, nieczytane: `ConvertedDocument.UnitLocations`
  (`IReadOnlyList<FaqUnitLocation>`, domyślnie `[]`) w `src/LegalAgent.Faq/Conversion/Model/ConversionModels.cs`;
  `FaqDocumentInput.UnitLocations` (domyślnie `[]`) i `FaqDocumentInput.MarkdownLink` (`string?`) w
  `src/LegalAgent.Faq/Model/FaqDocumentInput.cs`; to samo w `src/LegalAgent.Faq/Model/FaqSourceDocument.cs`. Puste
  szkielety metod rzucające `NotImplementedException`: `MarkdownAnchors.Slug(string)` i
  `MarkdownAnchors.ForDocument(string)` w nowym `src/LegalAgent.Faq/MarkdownAnchors.cs` (public static class),
  `UnitExtractor.Locate(LegalDocument, string)` w `src/LegalAgent.Faq/Conversion/UnitExtractor.cs`,
  `UnitMatcher.Find(string cited, IEnumerable<string> units)` (zwraca `string?`) w `src/LegalAgent.Faq/UnitMatcher.cs`. Build bez ostrzeżeń, stare
  testy zielone. Commit razem z testami red US1 (T007). **Uzasadnienie odstępstwa od konstytucji I**: bez tych typów
  testy red się nie kompilują; zadanie nie zmienia zachowania i trafia do commitu red (jak spec 012 T003).

**Checkpoint**: rozwiązanie się buduje, wszystkie istniejące testy przechodzą.

---

## Phase 3: User Story 1 — Link do strony PDF-u (Priority: P1) 🎯 MVP

**Goal**: nazwa dokumentu w wierszu źródła prowadzi do PDF-u na stronie, na której zaczyna się sekcja jednostki
(FR-1000, FR-1003–FR-1005).

**Independent Test**: FAQ z fałszywego modelu, jednostka „§ 2.” zaczyna się na stronie 2 syntetycznego regulaminu →
link nazwy kończy się `#page=2`; źródło bez jednostki → link bez `#page`.

### Tests for User Story 1 (red)

- [X] T003 [P] [US1] `tests/LegalAgent.Faq.Tests/UnitMatcherTests.cs`: testy `Find` — równa pozycja listy („§ 12” →
  „§ 12”), dłuższa („§ 12 ust. 3” → „§ 12”), numer nagłówka („6” → „6. Jakie rachunki prowadzimy?”), normalizacja
  (NBSP, kropka na końcu, wielkość liter), pierwsza w kolejności przy kilku pasujących, brak → `null`, pusty tekst →
  `null`; spójność: dla każdego przypadku `Find(...) != null` ⇔ `Matches(...)`.
- [X] T004 [P] [US1] Nowy `tests/LegalAgent.Faq.Tests/Conversion/UnitExtractorTests.cs` i fabryka wielostronicowa w
  `tests/LegalAgent.Faq.Tests/Fakes/TestPdfs.cs` (np. `RegulationOnPages(seed)`: „Rozdział 1.” i „§ 1.” na stronie 1,
  „§ 2.” na stronie 2, „§ 3.” na stronie 3, `SyntheticPdfBuilder`): `Locate(document, markdown)` daje jedną pozycję na
  jednostkę w kolejności `FromDocument` (`Unit` równe pozycji listy) ze stroną początku sekcji (1, 1, 2, 3; oznaczenie
  i tekst nagłówka tej samej sekcji mają tę samą stronę). Model z `PdfMarkdownConverter` na PDF-ie z fabryki.
- [X] T005 [P] [US1] `tests/LegalAgent.Faq.Tests/RendererTests.cs`: nowe testy (golden `faq.expected.md` bez zmian —
  dokumenty bez położeń dają wiersz jak dziś, SC-143): (a) dokument z `UnitLocations` [„§ 12”, strona 7] i źródło
  „§ 12” → `Źródło: [Regulamin A](https://example.test/pdf/a.pdf#page=7), § 12`; (b) jednostka „§ 12 ust. 3” → ta sama
  strona, tekst „§ 12 ust. 3”; (c) źródło bez jednostki → `[Regulamin A](https://example.test/pdf/a.pdf)`;
  (d) jednostka spoza listy położeń → bez `#page`; (e) `Page` = `null` → bez `#page`; (f) adres z fragmentem
  (`…a.pdf#x`) → bez dopisanego `#page`; (g) dwa źródła → oba z własną stroną, separator `; ` bez zmian;
  (h) front matter `resource` bez `#page` (FR-1005).
- [X] T006 [P] [US1] `tests/LegalAgent.Faq.Tests/FaqGeneratorTests.cs`: `FaqResult.Documents` niesie
  `UnitLocations` i `MarkdownLink` z `FaqDocumentInput` (D1…Dn w kolejności wejścia); `DocumentSetConverterTests`
  (`tests/LegalAgent.Faq.Tests/Conversion/DocumentSetConverterTests.cs`): `ConvertedDocument.UnitLocations` wypełnione
  (liczba = liczba `Units`, strony > 0); test aplikacji w `tests/mBank.FaqGenerator.Tests/FaqFlowTests.cs` (przebieg
  `AppHarness` z fałszywym modelem i PDF-em z fabryki wielostronicowej z T004 jako odpowiedzią `FakeHttpHandler`):
  każde źródło z jednostką w zapisanym FAQ ma link nazwy z `#page=N` równym stronie sekcji; front matter bez `#page`.
- [X] T007 [US1] Uruchomić `dotnet test tests/LegalAgent.Faq.Tests` (klasy z T003–T006): nowe testy padają z właściwego
  powodu (`NotImplementedException`, brak `#page`), stare przechodzą. Commit red (T002–T006):
  `test: FAQ source links to the PDF page (red, spec 013 T002-T006)`.

### Implementation for User Story 1 (green)

- [X] T008 [US1] `UnitMatcher.Find` w `src/LegalAgent.Faq/UnitMatcher.cs`: ta sama normalizacja co `Matches`, zwraca
  pierwszą w kolejności pozycję, dla której `StartsWithUnit` (pozycja albo jej numer nagłówka) jest prawdziwe;
  `Matches` przepisane na `Find(...) is not null` (bez zmiany zachowania).
- [X] T009 [US1] `UnitExtractor.Locate` w `src/LegalAgent.Faq/Conversion/UnitExtractor.cs`: przejście po sekcjach w
  kolejności `Collect` (oznaczenie, tekst nagłówka, dzieci), pozycja dla pierwszego wystąpienia jednostki ze stroną
  `Section.Pages.First`; `Anchor` na razie `null` (US2). `DocumentSetConverter` (`src/LegalAgent.Faq/Conversion/DocumentSetConverter.cs`)
  ustawia `ConvertedDocument.UnitLocations = UnitExtractor.Locate(document, markdown)`.
- [X] T010 [US1] `src/LegalAgent.Faq/FaqGenerator.cs`: `FaqSourceDocument` z `UnitLocations` i `MarkdownLink` wejścia.
  `src/LegalAgent.Faq/FaqMarkdownRenderer.cs` (`Sources`): położenie przez `UnitMatcher.Find(unit,
  document.UnitLocations.Select(l => l.Unit))`; nazwa `[Name](Resource#page=N)` gdy `Page` znane i adres bez `#`
  (`Resource.OriginalString`), inaczej jak dziś. `src/mBank.FaqGenerator/FaqStage.cs`: `FaqDocumentInput` z
  `UnitLocations = d.UnitLocations` (bez `MarkdownLink` — US2).
- [X] T011 [US1] Testy T003–T006 zielone; pełny `dotnet test LegalAgent.slnx --filter "Category!=Performance"`
  zielony (golden `faq.expected.md` bez zmian). Commit green (T008–T010):
  `feat: FAQ source names link to the PDF page of the unit (green, spec 013 T008-T010)`.

**Checkpoint**: US1 działa samodzielnie — FAQ ma linki do stron PDF-u.

---

## Phase 4: User Story 2 — Link do nagłówka w Markdownie (Priority: P2)

**Goal**: jednostka w wierszu źródła prowadzi do nagłówka jej sekcji w Markdownie dokumentu, ścieżką względną od
pliku FAQ (FR-1001–FR-1003, FR-1006).

**Independent Test**: FAQ z jednostką „§ 2.” ma `[§ 2.](../downloads/<plik>.md#-2)` (kotwica jak w VS Code), a
nagłówek o tej kotwicy istnieje w zapisanym Markdownie.

### Tests for User Story 2 (red)

- [X] T012 [P] [US2] Nowy `tests/LegalAgent.Faq.Tests/MarkdownAnchorsTests.cs`: `Slug` — tabela przykładów policzonych
  algorytmem VS Code (research R3: `trim`, `toLowerCase`, usunięcie znaków spoza L, M, Nd, Nl, Pc, `-`, białych znaków,
  każdy biały znak → `-`): „§ 26. Tytuł” → `-26-tytuł`; „8. Mobilna wpłata” → `8-mobilna-wpłata`; „Rozdział 1.
  Przepisy ogólne” → `rozdział-1-przepisy-ogólne`; „Art. 5¹” → `art-5`; „„Konto z premią”” → `konto-z-premią`;
  „A  B” (dwie spacje) → `a--b`; „snake_case” → `snake_case`; „Część II – Zasady” → `część-ii--zasady`.
  `ForDocument` — nagłówki ATX `#`–`######` w kolejności, tekst bez znaczników `#`, z usuniętymi ucieczkami `\)`,
  `\.` i znacznikami `**`/`*`; powtórzenia `x`, `x-1`, `x-2`; liczony także tytuł `#`; wiersze w blokach kodu i
  wiersze zaczynające się od `#` bez spacji nie są nagłówkami.
- [X] T013 [P] [US2] `tests/LegalAgent.Faq.Tests/Conversion/UnitExtractorTests.cs`: `Locate` daje `Anchor` nagłówka
  sekcji (oznaczenie i tekst nagłówka tej samej sekcji → ta sama kotwica); dwie sekcje o tym samym nagłówku → druga
  z dopiskiem `-1`, a jednostka (pierwsze wystąpienie) wskazuje pierwszą; Markdown z inną liczbą nagłówków sekcji niż
  sekcji (np. dopisany nagłówek) → wszystkie `Anchor` = `null`, strony zostają; SC-141: każda kotwica z `Locate` należy
  do `MarkdownAnchors.ForDocument(markdown)`. Dodatkowo (U2 z analizy): na aktach
  `tests/LegalAgent.PdfParser.Tests/Corpus/acts/*.pdf` (ścieżka przez `CallerFilePath`) i, jeśli dostępny z tego
  projektu testów, na syntetycznym dokumencie-tabeli — liczba nagłówków sekcji w Markdownie (bez tytułu) = liczba
  sekcji, czyli fallback z T018 nie wyłącza kotwic.
- [X] T014 [P] [US2] `tests/LegalAgent.Faq.Tests/RendererTests.cs`: `MarkdownLink` = `../downloads/a.md`, położenie
  „§ 12” z `Anchor` `-12-opłaty` → `Źródło: [Regulamin A](https://example.test/pdf/a.pdf#page=7), [§ 12](../downloads/a.md#-12-opłaty)`;
  tekst jednostki z `[`/`]` → `\[`, `\]`; `MarkdownLink` = `null` albo `Anchor` = `null` → jednostka zwykłym tekstem;
  usunięcie linków (zamiana `[t](adres)` na `t`) daje wiersz identyczny z wierszem bez położeń (SC-143).
- [X] T015 [P] [US2] Testy aplikacji w `tests/mBank.FaqGenerator.Tests/FaqFlowTests.cs`: (a) domyślne katalogi
  (`downloads`, `faq`) → link jednostki `../downloads/<plik>.md#kotwica`, kotwica ∈ `MarkdownAnchors.ForDocument` tego
  pliku (SC-141), 100% źródeł z jednostką ma oba linki (SC-140); (b) `--output` i `--faq-output` w różnych katalogach
  → ścieżka względna między nimi. Nowy `tests/mBank.FaqGenerator.Tests/MarkdownLinkTests.cs` dla
  `FaqStage.MarkdownLink(string faqDirectory, string markdownPath)` (`internal static`, zwraca `string?`; sprawdzić
  `InternalsVisibleTo`, inaczej dodać): na każdym systemie — katalogi rodzeństwa → `../downloads/a.md`; nazwa ze
  spacją i nawiasem → segment `regulamin%20%28a%29.md`; FAQ głębiej → `../../downloads/a.md`; tylko na Windows
  (`Assert.SkipUnless(OperatingSystem.IsWindows(), …)`) — `C:\faq` i `D:\dl\a.md` → `null` (FR-1003: bez linku do
  Markdownu, link `#page` zostaje).
- [X] T016 [US2] Uruchomić testy T012–T015: padają z właściwego powodu (`NotImplementedException` w `Slug`/`ForDocument`,
  brak linku jednostki). Commit red: `test: FAQ unit links to the Markdown heading (red, spec 013 T012-T015)`.

### Implementation for User Story 2 (green)

- [X] T017 [US2] `src/LegalAgent.Faq/MarkdownAnchors.cs`: `Slug` (research R3, kategorie `UnicodeCategory` .NET:
  litery, znaki łączące, `DecimalDigitNumber`, `LetterNumber`, `ConnectorPunctuation`, `-`, białe znaki → `-`) i
  `ForDocument` (nagłówki ATX poza blokami kodu ``` ` ``` ```, tekst inline bez ucieczek i `*`, licznik powtórzeń jak
  `github-slugger`: pierwsze bez dopisku, potem `-1`, `-2`, z pominięciem kotwic już zajętych).
- [X] T018 [US2] `UnitExtractor.Locate`: kotwice z `MarkdownAnchors.ForDocument(markdown)`; nagłówki sekcji = wszystkie
  nagłówki poza tytułem `#` (gdy `document.Title` niepusty, pierwszy nagłówek); k-ta sekcja w kolejności
  renderowania (pre-order) ↔ k-ty nagłówek sekcji; niezgodna liczba → `Anchor` = `null` wszędzie.
- [X] T019 [US2] `src/LegalAgent.Faq/FaqMarkdownRenderer.cs`: jednostka `[u](MarkdownLink#Anchor)` z ucieczką `[`/`]`,
  gdy `MarkdownLink` i `Anchor` znane, inaczej jak dziś. `src/mBank.FaqGenerator/FaqStage.cs` i
  `src/mBank.FaqGenerator/Program.cs`: przekazać katalog pobranych plików do `FaqStage.RunAsync`; nowa
  `internal static string? MarkdownLink(string faqDirectory, string markdownPath)` = `Path.GetRelativePath(...)`, gdy
  wynik nie jest ścieżką bezwzględną (`Path.IsPathRooted`): segmenty po `Path.DirectorySeparatorChar` /
  `AltDirectorySeparatorChar` złączone `/`, każdy przez `Uri.EscapeDataString` (`..` bez zmian); inaczej `null`.
  `FaqDocumentInput.MarkdownLink` = `MarkdownLink(faqDirectory, Path.Combine(downloads, d.MarkdownFileName))`.
- [X] T020 [US2] Testy T012–T015 zielone; pełny `dotnet test LegalAgent.slnx --filter "Category!=Performance"` zielony
  (golden `faq.expected.md` bez zmian). FR-1006 (C1 z analizy): nowy test regresji
  `tests/LegalAgent.Faq.Tests/LibraryIndependenceTests.cs` — pliki `src/LegalAgent.Faq/**/*.cs` (ścieżka przez
  `CallerFilePath`, bez `bin/`, `obj/`) nie zawierają „mbank” (bez względu na wielkość liter), „downloads” ani
  literału `http://`/`https://` poza komentarzami `///`; zielony od razu (odnotować w handoffie), w tym samym commicie.
  Commit green: `feat: FAQ units link to the heading in the document Markdown (green, spec 013 T017-T019)`.

**Checkpoint**: US1 i US2 działają; FAQ z obu linkami.

---

## Phase 5: Polish & Cross-Cutting Concerns

- [X] T021 [P] Kontrakt: w `specs/006-faq-generation/contracts/faq-file.md` przy „Wiersz źródeł” odsyłacz „od 1.5.0
  linki ze stroną i nagłówkiem — `specs/013-faq-source-links/contracts/faq-file-links.md`”; reszta bez zmian.
- [X] T022 [P] `src/LegalAgent.Faq/README.md` (API: `FaqUnitLocation`, `UnitExtractor.Locate`, `UnitMatcher.Find`,
  `MarkdownAnchors`, `FaqDocumentInput.UnitLocations`/`MarkdownLink`, sekcja wersji 1.5.0) i `src/mBank.FaqGenerator/README.md`
  (format wiersza źródła z przykładem; ograniczenie: w repozytorium linki do `downloads/*.md` działają lokalnie, nie
  na GitHubie; `#page=N` w Chrome/Edge/Firefox); `CLAUDE.md` (opis `LegalAgent.Faq`: linki źródeł, `MarkdownAnchors`).
- [X] T023 Wersja 1.5.0 w `Directory.Build.props`; `dotnet run --project src/LegalAgent.Corpus.Cli -c Release -- generate`
  i `verify` (zmienia się tylko wersja w `corpus/manifest.json` — sprawdzić `git diff --stat corpus`); pełny zestaw
  testów z `LEGALAGENT_PRIVATE_CORPUS` (bezwzględna ścieżka) zielony. Commit
  `chore: version 1.5.0, corpus manifest, docs for FAQ source links (spec 013 T021-T023)`.
- [ ] T024 Weryfikacja na prawdziwych dokumentach: bez Azure — konwersja pobranych regulaminów z
  `publish/mBank.FaqGenerator-win-x64/downloads` przez `DocumentSetConverter` w teście jednorazowym w scratchpadzie
  (albo `convert` CLI + `Locate`) i SC-141 dla wszystkich jednostek: każda kotwica ∈ nagłówki Markdownu, każda strona
  ∈ 1…liczba stron. Prawdziwy przebieg FAQ (Azure, klucz) i SC-142 (10 pytań: strona `#page` = strona nagłówka w PDF-ie)
  wykonuje właściciel — przygotować polecenie z quickstart 3; nowy `faq/FAQ_mBank.md` commitować tylko po jego
  przebiegu i zgodzie.
- [ ] T025 `publish/` jak w spec 012 T026 (NuGet 1.5.0 lokalnie w `publish/nuget/`, `publish/lib/*`,
  `mBank.FaqGenerator.exe` self-contained single-file skompresowany; bez ruszania `downloads/`, `faq/`,
  `appsettings*.json`). Usunąć pliki tymczasowe z repozytorium.
- [ ] T026 Handoff: `specs/013-faq-source-links/plan.md` „Stan prac i przekazanie” (wyniki T006, T011, T015, T020, T024,
  liczba testów), wszystkie zadania `[X]`; pamięć projektu. Commit `docs: spec 013 handoff (T026)`. Push i opis PR
  po potwierdzeniu właściciela (PR z bazą `012-footnote-original-labels`, dopóki 012 nie jest scalona).

---

## Dependencies & Execution Order

- **Setup (T001)** → **Foundational (T002)** → **US1** (red T003–T007 → green T008–T011) → **US2** (red T012–T016 →
  green T017–T020) → **Polish** (T021–T026).
- US2 zależy od US1 tylko przez wspólne pliki (`UnitExtractor.Locate`, renderer, `FaqStage`); logicznie niezależna.
- T023 przed T024–T025 (wersja 1.5.0 w plikach wykonywalnych).

### Parallel Opportunities

- T003, T004, T005, T006 — różne pliki testów (red US1).
- T012, T013, T014, T015 — różne pliki testów (red US2; T013 i T004 w tym samym pliku, ale w różnych fazach).
- T021, T022 — różne pliki dokumentacji.

## Parallel Example: User Story 1 (red)

```text
Task: "T003 UnitMatcher.Find w UnitMatcherTests.cs"
Task: "T004 Locate (strony) w Conversion/UnitExtractorTests.cs + TestPdfs.RegulationOnPages"
Task: "T005 renderer #page w RendererTests.cs"
Task: "T006 generator, konwerter i aplikacja niosą położenie"
```

## Parallel Example: User Story 2 (red)

```text
Task: "T012 MarkdownAnchorsTests.cs"
Task: "T013 Locate (kotwice) w UnitExtractorTests.cs"
Task: "T014 renderer: link jednostki"
Task: "T015 aplikacja: ścieżka względna i SC-141"
```

## Implementation Strategy

- **MVP**: Setup + T002 + US1 — linki do stron PDF-u, działają wszędzie (także na GitHubie).
- Potem US2 (linki do nagłówków, lokalnie), Polish (wersja, dokumentacja, weryfikacja na prawdziwych regulaminach,
  `publish/`).
- Proste zadania (T001, T021, T022, T025) można zlecić Sonnetowi; T008–T010, T017–T019 wykonuje główny agent.
