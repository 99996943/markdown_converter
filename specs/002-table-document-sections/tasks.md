---

description: "Task list for spec 002 — tabela-dokument"
---

# Tasks: Dokumenty zbudowane jako jedna wielostronicowa tabela dwukolumnowa (tabela-dokument)

**Input**: Design documents from `specs/002-table-document-sections/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/), [quickstart.md](./quickstart.md)

**Tests**: OBOWIĄZKOWE — konstytucja, zasada I (TDD, NON-NEGOTIABLE). Zadanie testowe poprzedza
implementację i MUSI najpierw padać **na asercji** (nie na kompilacji — w razie potrzeby szkielet typów
z `NotImplementedException`, niezarejestrowany w potoku). Osobne commity: `test: … (red)`, potem
`feat:`/`fix: …` (green). Testy offline i deterministyczne; prawdziwe PDF banku wyłącznie w
`Corpus/private` (poza git) i w teście opcjonalnym.

**Organization**: Zadania pogrupowane wg historyjek ze spec.md: US1 (P1) sekcje, US2 (P1) kolumny z
siatki, US3 (P2) brak fałszywych nagłówków, US4 (P1) brak regresji.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: można wykonać równolegle (inne pliki, brak zależności od niezakończonych zadań)
- **[Story]**: historyjka (US1…US4)

## Path Conventions

- Biblioteka: `src/LegalAgent.PdfParser/`; testy: `tests/LegalAgent.PdfParser.Tests/` (dalej `tests/`).
- Syntetyczne PDF wyłącznie przez `tests/Fixtures/SyntheticPdfBuilder.cs` i `tests/Fixtures/BankingCorpusGenerator.cs`.
- Konwencje jak w 001: `CultureInfo.InvariantCulture` / `StringComparison.Ordinal*`, jawne sortowania,
  LF, XML-doc publicznych typów, wzorce etapów jak w `StepSequenceStage` (FR-067).
- Odniesienia „R*n*” = decyzje w research.md; „FR-0xx” bez spec 002 = spec 001.

---

## Phase 1: Setup

- [X] T001 Zapisz punkt odniesienia wyników sprzed zmiany (quickstart.md §0): skonwertuj CLI `mbank-regulamin-pdp`, `mbank-reg1`, `mbank-reg2`, `mbank-reg3` do `tests/Corpus/private/baseline/<nazwa>.md` (+ `--report … .report.json`); potwierdź `git status`, że nic z `Corpus/private` nie jest śledzone
- [X] T002 [P] Dodaj `NotoSansMono-Regular.ttf` (OFL, z repozytorium Noto Fonts) do `tests/Fixtures/Fonts/` (kopiowanie do wyjścia już obejmuje `Fixtures\Fonts\*`) i napisz test w `tests/Fixtures/SyntheticPdfBuilderTests.cs`: `Text(x, y, "o", mono: true)` daje literę, której `FontName` z PdfPig zawiera „NotoSansMono”, a zwykły `Text` — „NotoSans” bez „Mono” (red)
- [X] T003 Dodaj parametr `bool mono = false` do `Text(...)` w `tests/Fixtures/SyntheticPdfBuilder.cs` (czcionka mono ładowana jak pozostałe kroje) — T002 green

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: typy publiczne i wewnętrzne, opcje, dokumenty syntetyczne i rozpoznanie regionu tabeli-dokumentu — wspólne dla US1–US4.

**⚠️ CRITICAL**: żadna historyjka nie startuje przed końcem tej fazy.

- [X] T004 [P] Testy w `tests/Unit/Options/PdfParserOptionsValidatorTests.cs` (red): wartości domyślne `Tables.DetectTableDocuments = true`, `TableDocumentMaxLeftColumnRatio = 0.35`, `TableDocumentMinPages = 2`, `TableDocumentMinPageRatio = 0.5`, `TableDocumentMinMedianWords = 40`, `Headings.DetectImageCaptions = true`, `Headings.ValidityLineAsParagraph = true` są poprawne; odrzucane: `TableDocumentMaxLeftColumnRatio` i `TableDocumentMinPageRatio` spoza „(0, 1]”, `TableDocumentMinPages` < 2 („≥ 2”), `TableDocumentMinMedianWords` < 1 („≥ 1”); `Clone()` kopiuje nowe pola
- [X] T005 Dodaj pola do `TableOptions` i `HeadingOptions` w `src/LegalAgent.PdfParser/Options/PdfParserOptions.cs` (XML-doc z numerami FR) i reguły w `src/LegalAgent.PdfParser/Options/PdfParserOptionsValidator.cs` — T004 green
- [X] T006 [P] Testy w `tests/Unit/Pipeline/ConversionReportTests.cs` (red): `ConversionReport.TableDocuments` domyślnie pusta lista; `ReportBuilder.AddTableDocument(firstPage, lastPage, sectionCount, headerRowText, droppedHeaderRows)` → `TableDocumentSummary` w raporcie, posortowane po `FirstPage`; `TableCount`/`FallbackTableCount` nie zmieniają się; raport JSON CLI (`tests/Integration/CliTests.cs`) zawiera `tableDocuments`
- [X] T007 Model publiczny wg data-model.md §1 i contracts/public-api.md: `SectionKind.TableDocumentSection` (nowa wartość **na końcu** enum, XML-doc) w `src/LegalAgent.PdfParser/Model/Section.cs`; rekord `TableDocumentSummary(int FirstPage, int LastPage, int SectionCount, string? HeaderRowText, int DroppedHeaderRows)` i właściwość `init` `TableDocuments` (domyślnie `[]`) w `src/LegalAgent.PdfParser/Model/ConversionReport.cs`; `AddTableDocument` w `src/LegalAgent.PdfParser/Pipeline/ReportBuilder.cs` — T006 green
- [X] T008 [P] Typy wewnętrzne (bez testu — same deklaracje): `StageOrder.TableDocument = 560` w `src/LegalAgent.PdfParser/Pipeline/StageOrder.cs`; `LayoutAnnotations.TableDocumentIndex = "tabledoc.index"` w `src/LegalAgent.PdfParser/Layout/LayoutAnnotations.cs`; `internal sealed record TableDocumentRegion(int Index, int FirstPage, int LastPage, double Top, double Divider, double ContentLeft, double ContentRight, int SectionCount, string? HeaderRowText, int DroppedHeaderRows)` i lista `PipelineContext.TableDocuments` w `src/LegalAgent.PdfParser/Pipeline/PipelineContext.cs`; szkielet `src/LegalAgent.PdfParser/Stages/TableDocumentStage.cs` (`Order => StageOrder.TableDocument`, `Execute` rzuca `NotImplementedException`), **niezarejestrowany** w `BuiltInStages`
- [X] T009 [P] Dokument syntetyczny w `tests/Fixtures/BankingCorpusGenerator.cs` (metoda `RegulaminPromocjiTabela`, dostępny przez `BankingCorpusGenerator.Pdf(name)`/`Truth(name)`, **jeszcze nie w `Names`**): okładka — tytuł pogrubiony 20 pt w 2 liniach, „Obowiązuje od 01.09.2026 r. do 30.11.2026 r.” 12 pt, `Image` 230×220 pt i pod nim (17 pt odstępu) pogrubiony podpis „bank.example”; strony 2–5 — tabela z siatką jak w research.md „Pomiary” (pionowe `VLine` x 54/181/541, poziome linie w dwóch kawałkach 55–181 i 181–541), wiersz nazw kolumn „Definicje | Wyjaśnienie” pogrubiony na str. 2, 3 i 5 (bez niego na str. 4), sekcje: „Organizator promocji” (akapit), „Uczestnik promocji” (lista „•”, akapit po liście, pogrubiony śródtytuł „Nie możesz uczestniczyć w promocji, jeśli:”, lista), „Ważne pojęcia” (5 definicji „termin – objaśnienie” bez punktorów, nierówny prawy brzeg), „Korzyści promocji” (nazwa w 2 liniach; nazwa i pierwsza linia treści na tej samej linii bazowej; pogrubiony śródtytuł w 2 liniach; „•” z podpunktami „o” w kroju mono x 226 / tekst x 244; słowo „o” w zwykłym kroju na początku linii kontynuacji; komórka przechodzi ze str. 3 na 4 (akapit przerwany w środku zdania) i z 4 na 5 (pozycja „•” przerwana w środku zdania, kontynuacja z wcięciem tekstu pozycji), obie z pustą lewą komórką), „Warunki/zasady promocji” (nazwa przerwana granicą strony: „Warunki/zasady” w ostatniej linii ramki str. 4, „promocji” na górze str. 5), „Jak możesz złożyć reklamację dotyczącą promocji?” (nazwa w 5 liniach, linie nazwy o 1 pt niżej niż linie treści, lista „1)”–„3)”, adres `https://example.org/products/pierscien-platniczy-` / `mastercard` z krótkim podkreśleniem pod adresem); strona 6 bez siatki — pogrubione „MOJE OŚWIADCZENIA”, lista „1)”, „2)”, linia kropek, „data, miejsce i podpis Uczestnika promocji”; numery stron „N/5” w stopce. `DocumentTruth` rozszerzony o `SectionNames` (kolejność) i `HeaderRowWords`. Testy w `tests/Fixtures/BankingCorpusGeneratorTests.cs`: PDF się otwiera, 6 stron, str. 2–5 mają 3 pionowe linie siatki, `Truth` zawiera 6 nazw sekcji
- [X] T010 [P] Dokument negatywny `regulamin-z-tabela-definicji` w `tests/Fixtures/BankingCorpusGenerator.cs` (też jeszcze nie w `Names`): 6 stron zwykłego regulaminu (nagłówki, akapity), na str. 3–4 tabela z siatką „Definicje | Wyjaśnienie” z 12 krótkimi wierszami (termin pogrubiony | objaśnienie 8–20 słów), lewa kolumna 25% — spełnia (a), (b), nie spełnia (c) i (e); test otwarcia w `BankingCorpusGeneratorTests.cs`
- [X] T011 Testy rozpoznania regionu w nowym `tests/Unit/Stages/TableDocumentStageTests.cs` (red; `LayoutFactory`/`StageHarness`, strony z `Rulings`): (R2) ramka = 3 pionowe linie o wspólnym zakresie; poziome kawałki łączone; granica wiersza tylko gdy kawałki pokrywają ≥ 90% szerokości obu kolumn — podkreślenie w prawej kolumnie i wypełniony prostokąt w komórce nie dzielą wiersza; (R3) region z 2+ kolejnych stron o zgodnych X (±3 pt) — rozpoznany, gdy spełnia (a)–(e); NIE rozpoznany, gdy: 3 kolumny; lewa kolumna 40% szerokości; 1 strona; 2 strony z 6 stron z tekstem (< 50%); mediana prawych komórek < 40 słów; brak listy, akapitów i przejścia przez stronę; strona z liniami schematu kroków (`step.scheme`) przerywa region (FR-089); `DetectTableDocuments = false`. Rozpoznany region → `PipelineContext.TableDocuments` z `FirstPage`, `LastPage`, `Top`, `Divider`, a wszystkie linie w ramce mają `tabledoc.index`
- [X] T012 Zaimplementuj rozpoznanie ramki, granic wierszy i regionu (R2, R3) w `src/LegalAgent.PdfParser/Stages/TableDocumentStage.cs` (prywatne `PageFrame`, `FrameRow`); progi z `TableOptions`; anulowanie sprawdzane per strona — T011 green

**Checkpoint**: region tabeli-dokumentu rozpoznawany w testach jednostkowych; etap nadal niezarejestrowany, wynik biblioteki bez zmian (pełny zestaw testów zielony).

---

## Phase 3: User Story 1 — Sekcje tabeli-dokumentu jako nagłówki z ciągłą treścią (Priority: P1) 🎯 MVP

**Goal**: każda nazwa z lewej kolumny jest nagłówkiem `##`, a pod nią cała treść prawej komórki jako akapity i listy, scalona przez wiersze i strony; wiersz nazw kolumn pominięty.

**Independent Test**: `regulamin-promocji-tabela` (T009) → nagłówki = `Truth.SectionNames` w kolejności, pod każdym treść bez tabel; definicje osobnymi akapitami; „o” zagnieżdżone pod „•”; akapit przerwany granicą strony jest jednym akapitem ze znacznikiem strony.

### Tests for User Story 1 ⚠️

- [X] T013 [P] [US1] Testy w `tests/Unit/Stages/TableDocumentStageTests.cs` (red): (R4) wiersz nazw kolumn na pierwszej stronie i jego powtórzenia (ten sam odcisk) → `LineRole.Artifact`, brak powtórzenia na stronie nie psuje rozpoznania, pierwszy wiersz z długą treścią NIE jest wierszem nazw; (R5) linia obejmująca obie kolumny jest dzielona na granicy (`CenterX < Divider`); (R6) lewe linie wiersza → pierwsza `Role = Heading` z `HeadingInfo(Level 2, TableDocumentSection, Designation null, Number null, Title = Text = nazwa)`, kolejne `Role = Heading` bez informacji; nazwa wieloliniowa złączona spacją bez znaczników; nazwa przerwana stroną (ostatnia lewa linia ≤ 1,5 wysokości linii od dołu ramki + niepusta lewa komórka pierwszego wiersza następnej strony) → jeden nagłówek; pusta lewa komórka → brak nagłówka (kontynuacja); pierwszy wiersz danych regionu z pustą lewą komórką → jego treść zostaje bez nagłówka (treść wstępna przed pierwszą sekcją, bez dopisanej nazwy — FR-084, FR-091); (R7) `page.Lines` w kolejności: artefakty wiersza nazw, nazwa, treść — wiersz po wierszu; linie poza ramką na swoich miejscach; prawe linie mają `column.left`/`column.right` = skrajne brzegi treści regionu; `TableDocumentRegion.SectionCount`, `HeaderRowText` („Definicje | Wyjaśnienie”), `DroppedHeaderRows`
- [X] T014 [P] [US1] Testy w `tests/Unit/Stages/BlockAssemblyStageTests.cs` (red, R9): dla linii z `tabledoc.index` — linia, za którą zmieściłoby się pierwsze słowo następnej linii (wolne miejsce do `column.right` ≥ szerokość słowa + odstęp międzywyrazowy), kończy akapit bez kropki („Bank – mBank S.A.” / „Rachunek bieżący – …” → 2 akapity); linia z nierównym prawym brzegiem, za którą słowo by się nie zmieściło, kontynuuje akapit; zmiana „cała linia pogrubiona” ↔ „nie cała” kończy akapit; dwie pogrubione linie po sobie → jeden akapit; linie bez `tabledoc.index` — zachowanie bez zmian (istniejące testy zielone)
- [X] T015 [P] [US1] Testy punktora „o” (red, R10): `tests/Unit/Stages/PageExtractionStageTests.cs` — `LayoutGlyph.FontName` = nazwa czcionki litery z PdfPig (syntetyczny PDF z krojem mono); `tests/Unit/Stages/ListDetectionStageTests.cs` — pierwsze słowo „o” w innej rodzinie czcionki niż następne słowo (rodzina = nazwa bez prefiksu `ABCDEF+` i bez przyrostka po „-” lub „,”) → pozycja `Bullet` z oznaczeniem „o”, zagnieżdżona wg wcięcia pod „•”; „o” w tej samej rodzinie co tekst → zwykłe słowo; „o” bez tekstu za nim → nie punktor
- [X] T016 [P] [US1] Testy FR-094 w `tests/Unit/Text/HyphenationTests.cs` (red): „…/pierscien-platniczy-” + „mastercard” → `Compound`; „www.example.org/a-” + „b” → `Compound`; „https://x.pl/rozwijaj-” + „firme” → `Compound`; zwykły wyraz „Zagra-” + „nicznych” → `Soft` (bez zmian)
- [X] T017 [US1] Test integracyjny w nowym `tests/Integration/TableDocumentsIntegrationTests.cs` (red): konwersja `regulamin-promocji-tabela` → nagłówki `##` dokładnie = `Truth.SectionNames` w kolejności; między nagłówkami brak `|`; „Definicje”/„Wyjaśnienie” nie występują w wyniku; sekcja „Korzyści promocji” zawiera treść ze str. 3–5 z `<!-- page: 4 -->` wewnątrz akapitu przerwanego granicą strony; podpunkty „o” jako `  - …` pod pozycją „•”; pozycja „•” przerwana granicą str. 4/5 jest jedną pozycją listy z `<!-- page: 5 -->` wewnątrz; definicje „Ważne pojęcia” jako 5 akapitów; adres `https://example.org/products/pierscien-platniczy-mastercard`; model: sekcje `Kind = TableDocumentSection`, `Report.TableDocuments` = 1 pozycja (str. 2–5, `SectionCount` 6, `HeaderRowText` „Definicje | Wyjaśnienie”, `DroppedHeaderRows` 3), `TableCount` = 0
- [X] T018 [US1] Testy pomijania linii tabeli-dokumentu (red): w `tests/Unit/Stages/TableDetectionStageTests.cs` i `tests/Unit/Stages/ReadingOrderStageTests.cs` — linie z `tabledoc.index` nie trafiają do tabeli ani do kolumn tekstu, a linie bez adnotacji na tej samej stronie (np. pod ramką) są przetwarzane jak dotąd; w `tests/Integration/TableDocumentsIntegrationTests.cs` — strona tabeli-dokumentu jak str. 4 i 11 `mbank-reg3` (przerwy ≥ 2× odstęp międzywyrazowy, np. „w EUR (SEPA) do krajów …”, linie lewej kolumny o 1 pt niżej niż prawej, 3 krótkie podkreślenia linków i wypełniony prostokąt pod linkiem w prawej komórce) → wynik bez `|` i ` \| `, zdania w oryginalnej kolejności, `Report.Warnings` bez `TBL001_AmbiguousGrid`, `FallbackTableCount` = 0

### Implementation for User Story 1

- [X] T019 [US1] W `src/LegalAgent.PdfParser/Stages/TableDocumentStage.cs` zaimplementuj wiersz nazw kolumn (R4), rozcinanie linii przez `LineSlicer` (R5), nazwy sekcji jako nagłówki (R6), przestawienie linii i kolumnę treści (R7) oraz `context.Report.AddTableDocument(...)` — T013 green
- [X] T020 [US1] Dodaj `string? FontName = null` do `src/LegalAgent.PdfParser/Layout/LayoutGlyph.cs`, wypełniaj z `Letter.FontName` w `src/LegalAgent.PdfParser/Stages/PageExtractionStage.cs`, rozpoznaj punktor „o” w `src/LegalAgent.PdfParser/Stages/ListDetectionStage.cs` (pomocnik rodziny czcionki w `src/LegalAgent.PdfParser/Text/ListLabelPatterns.cs` lub nowym `Text/FontFamily.cs`) — T015 green
- [X] T021 [P] [US1] Reguła adresu (FR-094) w `src/LegalAgent.PdfParser/Text/Hyphenation.cs` — T016 green
- [X] T022 [US1] Reguły R9 w `ContinuesParagraph` w `src/LegalAgent.PdfParser/Stages/BlockAssemblyStage.cs` (tylko linie z `tabledoc.index`) — T014 green
- [X] T023 [US1] Zarejestruj `TableDocumentStage` w `src/LegalAgent.PdfParser/Pipeline/BuiltInStages.cs`; w `src/LegalAgent.PdfParser/Stages/TableDetectionStage.cs` i `src/LegalAgent.PdfParser/Stages/ReadingOrderStage.cs` pomijaj linie z `tabledoc.index` (wzór: `step.scheme`); uruchom pełny zestaw — T017, T018 green, golden `Corpus/acts` i `Corpus/banking` **bez zmian**
- [X] T024 [US1] Weryfikacja na `tests/Corpus/private/mbank-reg3.pdf` (CLI, quickstart §2): nagłówki sekcji, ciągłość „Korzyści promocji” przez str. 4–6, „Ważne pojęcia”, „o”, adresy; każdy znaleziony błąd → najpierw czerwony przypadek w `TableDocumentStageTests`/`TableDocumentsIntegrationTests` (odtworzony syntetycznie), potem poprawka; wpisz wynik do sekcji „Stan prac” w `specs/002-table-document-sections/plan.md`

**Checkpoint**: US1 działa — syntetyczna tabela-dokument daje sekcje; `mbank-reg3` ma nazwy sekcji jako `##` (nagłówki ze śródtytułów mogą jeszcze zostać — US3).

---

## Phase 4: User Story 2 — Kolumny wyznacza siatka, nie justowanie (Priority: P1)

**Goal**: szerokie odstępy międzywyrazowe i podkreślenia linków w tabeli-dokumencie nie tworzą kolumn, wierszy ani tabel awaryjnych.

**Independent Test**: strona tabeli-dokumentu z nierównymi odstępami międzywyrazowymi i podkreśleniami linków → akapit z pełnymi zdaniami, brak `|`, brak `TBL001_AmbiguousGrid` w raporcie.

(Testy tej historyjki są w US1 — T018 — bo jej zachowanie dostarcza rejestracja etapu i pomijanie linii w T023; zgodnie z zasadą I test czerwony poprzedza ten kod.)

- [X] T025 [US2] Weryfikacja na `tests/Corpus/private/mbank-reg3.pdf` str. 4 i 11 (quickstart §2): brak trybu awaryjnego i fałszywych kolumn, słowa w zdaniach; każdy znaleziony błąd → najpierw czerwony przypadek syntetyczny w `tests/Integration/TableDocumentsIntegrationTests.cs`, potem poprawka w `src/LegalAgent.PdfParser/Stages/TableDocumentStage.cs`

**Checkpoint**: US1 + US2 — `mbank-reg3` bez tabel GFM, bez tabel awaryjnych i bez fałszywych kolumn (SC-013).

---

## Phase 5: User Story 3 — Brak fałszywych nagłówków w tabeli-dokumencie i na okładce (Priority: P2)

**Goal**: śródtytuły w komórkach, tekst za tabelą („MOJE OŚWIADCZENIA”), podpis grafiki i „Obowiązuje od …” nie są nagłówkami.

**Independent Test**: `regulamin-promocji-tabela` → jedyne nagłówki to `#` tytuł i `##` nazwy sekcji; śródtytuły i „MOJE OŚWIADCZENIA” jako `**…**`; „Obowiązuje od …” i podpis pod obrazem jako akapity w preambule.

### Tests for User Story 3 ⚠️

- [X] T026 [P] [US3] Testy w `tests/Unit/Stages/PageExtractionStageTests.cs` (red): `LayoutPage.ImageAreas` zawiera prostokąt obrazu z syntetycznego PDF (Y w dół, zgodny z `Image(x, y, w, h)` ±1 pt); strona bez obrazów → lista pusta
- [X] T027 [P] [US3] Testy w `tests/Unit/Stages/HeadingDetectionStageTests.cs` (red): (FR-087/086) gdy `PipelineContext.TableDocuments` niepuste — linie od strony i górnej krawędzi ramki pierwszej tabeli-dokumentu do końca dokumentu (także pogrubione, wersalikowe, powiększone i za tabelą) nie są nagłówkami typograficznymi ani blokiem tytułowym aktu; „Art. 5.” nadal jest jednostką; linie przed tabelą — bez zmian; (FR-088) linia 1–2-liniowa, której prostokąt zachodzi na obraz albo której górna krawędź leży ≤ 3 wysokości linii pod obrazem i której zakres poziomy mieści się w obrazie poszerzonym o 10% z każdej strony → nie jest nagłówkiem ani częścią tytułu; ta sama linia 5 wysokości linii pod obrazem lub poza jego szerokością → reguła nie działa; `DetectImageCaptions = false` → zachowanie 001; (FR-093) linia pierwszej strony za tytułem, przed pierwszym nagłówkiem, pasująca do `^obowiązuje\s+od\b` bez względu na wielkość liter → nie jest nagłówkiem ani częścią tytułu; ta sama fraza w treści dalej → bez znaczenia; `ValidityLineAsParagraph = false` → zachowanie 001
- [X] T028 [US3] Test integracyjny w `tests/Integration/TableDocumentsIntegrationTests.cs` (red): `regulamin-promocji-tabela` → `grep ^#` = tytuł + `Truth.SectionNames`; „**Nie możesz uczestniczyć w promocji, jeśli:**” i dwuliniowy śródtytuł „Korzyści …” jako jeden pogrubiony akapit; „**MOJE OŚWIADCZENIA**”, lista oświadczeń i linia podpisu na końcu ostatniej sekcji; „Obowiązuje od 01.09.2026 r. do 30.11.2026 r.” i „bank.example” jako akapity w `Document.Preamble`

### Implementation for User Story 3

- [X] T029 [US3] Dodaj `LayoutPage.ImageAreas` w `src/LegalAgent.PdfParser/Layout/LayoutPage.cs` i wypełniaj z `Page.GetImages()` (`Bounds`, Y w dół) w `src/LegalAgent.PdfParser/Stages/PageExtractionStage.cs` — T026 green
- [X] T030 [US3] W `src/LegalAgent.PdfParser/Stages/HeadingDetectionStage.cs`: wykluczenie kandydatów od początku tabeli-dokumentu (FR-087), podpis grafiki (FR-088), linia „obowiązuje od” (FR-093; regex `GeneratedRegex` z `IgnoreCase | CultureInvariant`) — także w `MergeTitleBlock` — T027, T028 green
- [X] T031 [US3] Pełny zestaw: golden `Corpus/acts` i `Corpus/banking` bez zmian (w `regulamin-rachunku` linia „*obowiązuje od …*” już jest akapitem); weryfikacja `mbank-reg3.pdf` — brak `### Nie możesz…`, `### Korzyści obowiązujące po okresie 24 miesięcy:`, `### MOJE OŚWIADCZENIA`, `## mBank.pl`, `## Obowiązuje od …` (SC-012)

**Checkpoint**: US1–US3 — wynik `mbank-reg3` spełnia SC-010 – SC-015 (do potwierdzenia metrykami w Polish).

---

## Phase 6: User Story 4 — Zwykłe tabele i dotychczasowe dokumenty bez zmian (Priority: P1)

**Goal**: taryfy, tabela definicji, schematy kroków i akty wychodzą jak dotąd; jedyna zamierzona zmiana w prywatnych regulaminach to FR-093.

**Independent Test**: `regulamin-z-tabela-definicji` → tabela GFM; prywatne `mbank-regulamin-pdp`, `mbank-reg1`, `mbank-reg2` = baseline z poprawką FR-093; `DetectTableDocuments = false` → `mbank-reg3` jak w 001.

- [X] T032 [P] [US4] Test integracyjny w `tests/Integration/TableDocumentsIntegrationTests.cs`: `regulamin-z-tabela-definicji` → `Report.TableDocuments` puste, `TableCount` = 1, tabela GFM z 12 wierszami; `regulamin-promocji-tabela` z `DetectTableDocuments = false` → brak sekcji `TableDocumentSection`, tabele jak w 001 (red tylko jeśli nie przechodzi — wtedy poprawka w `TableDocumentStage.cs`)
- [ ] T033 [US4] Utwórz `tests/Corpus/private/mbank-regulamin-pdp.expected.md`, `mbank-reg1.expected.md`, `mbank-reg2.expected.md` (poza git) jako kopie `baseline/*.md` z **jedyną** ręczną zmianą FR-093 (linia `## obowiązuje od …` → `obowiązuje od …`); uruchom `PrivateCorpusTests` z `LEGALAGENT_PRIVATE_CORPUS` (quickstart §1, §3); każda inna różnica → czerwony przypadek syntetyczny + poprawka (FR-088 i reguła „o” to najbardziej prawdopodobne źródła)
- [X] T034 [US4] Sprawdź wyłączenie (quickstart §4): `PDFPARSER__Tables__DetectTableDocuments=false` dla `mbank-reg3.pdf` → wynik jak `baseline/mbank-reg3.md` poza zmianami FR-088, FR-093, FR-094 i „o”; zapisz obserwacje w `specs/002-table-document-sections/plan.md`

**Checkpoint**: wszystkie historyjki gotowe; brak regresji (SC-016).

---

## Phase 7: Polish & Cross-Cutting Concerns

- [X] T035 Napisz ręcznie (wg spec i `Truth`, nie z wyniku) `tests/Corpus/banking/regulamin-promocji-tabela.expected.md` i `tests/Corpus/banking/regulamin-z-tabela-definicji.expected.md`, dodaj oba dokumenty do `Names` w `tests/Fixtures/BankingCorpusGenerator.cs`; `GoldenTests` zielone (różnice → analiza: błąd biblioteki = test red + poprawka, błąd oczekiwań = poprawka pliku z uzasadnieniem w `tests/Corpus/REVIEW.md`)
- [ ] T036 [P] Metryki w `tests/Corpus/QualityMetricsTests.cs` dla `regulamin-promocji-tabela`: SC-010 (100% słów PDF poza `Truth.HeaderRowWords` w wyniku, w kolejności; brak słów spoza PDF), SC-011 (nagłówki `##` = `Truth.SectionNames`, każdy przed swoją treścią), SC-012 (nagłówki = tytuł + nazwy sekcji), SC-013 (0 tabel, 0 ` \| `, 0 `TBL001`), SC-014 (treść komórek przechodzących przez stronę w jednym fragmencie między nagłówkami), SC-015 (oznaczenia i głębokość list = `Truth.ListItems`)
- [ ] T037 [P] Niezmienniki 7–9 z `contracts/markdown-output.md` w `tests/Rendering/MarkdownInvariantsTests.cs` (na korpusie syntetycznym)
- [ ] T038 Utwórz `tests/Corpus/private/mbank-reg3.expected.md` (poza git) po przeglądzie wyniku wg quickstart §2 (sprawdzenie kompletności słów względem surowego tekstu PdfPig); rozszerz `tests/Corpus/PrivateCorpusTests.cs` o asercje dla dokumentów z tabelą-dokumentem: 0 linii zaczynających się od `|`, 0 ` \| `, `Report.TableDocuments` niepuste
- [X] T039 [P] Dokumentacja: README (opis tabeli-dokumentu, nowe opcje `Tables.DetectTableDocuments` i progi, `Headings.DetectImageCaptions`, `Headings.ValidityLineAsParagraph`, nowa wartość `SectionKind`, wskazówka dla chunkera), XML-doc nowych typów, `specs/001-legal-pdf-parser/contracts/public-api.md` — odnośnik do kontraktu 1.1.0, `tests/Corpus/REVIEW.md` — wpis dla nowych golden
- [ ] T040 Pełny zestaw `dotnet test LegalAgent.slnx -c Release` (w tym `Performance`, `DeterminismTests`), CI Linux zielone (SC-017); notatki przekazania w `specs/002-table-document-sections/plan.md` („Stan prac i przekazanie”) i aktualizacja `tasks.md`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Ph1)** → **Foundational (Ph2)** → blokuje wszystkie historyjki.
- **US1 (Ph3)**: po Ph2. Rejestruje etap w potoku (T023) — od tego momentu zmienia się wynik biblioteki.
- **US2 (Ph4)**: po US1 (korzysta z zarejestrowanego etapu i pomijania linii).
- **US3 (Ph5)**: po Ph2; FR-088/FR-093 (T026, T027 częściowo, T029, T030) niezależne od US1, FR-087 i T028 wymagają US1.
- **US4 (Ph6)**: po US1–US3 (porównuje końcowy wynik); T032 może iść zaraz po US1.
- **Polish (Ph7)**: po wszystkich historyjkach.

### Graf

```text
Ph1 → Ph2 → US1 ─┬─► US2 ─┐
                 └─► US3 ─┼─► US4 ─► Polish
      Ph2 ─► US3 (FR-088, FR-093) ┘
```

### Within Each User Story

- Test czerwony (commit `(red)`) → implementacja (commit green) → pełny zestaw testów.
- Pliki współdzielone (`TableDocumentStage.cs`, `HeadingDetectionStage.cs`, `BlockAssemblyStage.cs`,
  `PageExtractionStage.cs`) zmieniane sekwencyjnie.
- Po każdej historyjce: weryfikacja na `mbank-reg3.pdf` i szybkie porównanie prywatnych regulaminów z
  `baseline/` (wykrycie regresji wcześnie, nie dopiero w US4).

### Parallel Opportunities

- Ph1: T002 równolegle z T001.
- Ph2: T004, T006, T008, T009, T010 równolegle; T005/T007 po swoich testach; T011 → T012 po T008.
- US1: testy T013, T014, T015, T016 równolegle; T017 i T018 po T009 (wspólny plik `TableDocumentsIntegrationTests.cs` — kolejno); T021 równolegle z T019/T020.
- US3: T026 i T027 równolegle; FR-088/FR-093 można robić równolegle z US1 (inne pliki poza `PageExtractionStage.cs` — koordynować z T020).
- Polish: T036, T037, T039 równolegle.

---

## Parallel Example: User Story 1

```text
# Red — testy jednocześnie:
T013 TableDocumentStageTests   T014 BlockAssemblyStageTests
T015 PageExtraction/ListDetection („o”)   T016 HyphenationTests
T018 TableDetection/ReadingOrder (pomijanie) + strona jak str. 4 i 11

# Green — niezależne pliki:
T019 TableDocumentStage   T021 Hyphenation
→ T020 („o”), T022 (BlockAssembly), T023 (rejestracja + pomijanie)
```

## Parallel Example: User Story 3

```text
T026 PageExtractionStageTests (ImageAreas)   T027 HeadingDetectionStageTests
→ T029 PageExtractionStage   → T030 HeadingDetectionStage
```

---

## Implementation Strategy

### MVP First (US1)

1. Ph1 + Ph2 (wynik biblioteki bez zmian — etap niezarejestrowany).
2. US1 → **STOP i walidacja**: `mbank-reg3` ma sekcje `##` z ciągłą treścią; golden bez zmian.

### Incremental Delivery

1. MVP (US1) → chunker dzieli regulamin promocji po sekcjach.
2. + US2 → brak fałszywych kolumn i trybu awaryjnego.
3. + US3 → brak fałszywych nagłówków (śródtytuły, oświadczenia, okładka).
4. + US4 → potwierdzony brak regresji; + Polish → golden, metryki SC, README, CI.

### Notes

- [P] = różne pliki, brak zależności od niezakończonych zadań.
- Implementacja wyłącznie przez `/speckit-implement` z zakresem zadań; po każdej fazie odhaczanie `[X]`.
- Zmiana zachowania sprzecznego ze spec → najpierw doprecyzowanie FR w spec.md (w tym samym commicie co test red).
- Wynik zawiera wyłącznie oryginalny tekst PDF — żadnych dopisanych etykiet (FR-091).
