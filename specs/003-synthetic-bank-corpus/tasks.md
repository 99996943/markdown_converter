---

description: "Task list for spec 003 — syntetyczny korpus banku"
---

# Tasks: Syntetyczny korpus dokumentów fikcyjnego banku dla aplikacji RAG

**Input**: Design documents from `specs/003-synthetic-bank-corpus/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/), [quickstart.md](./quickstart.md)

**Tests**: OBOWIĄZKOWE — konstytucja, zasada I (TDD, NON-NEGOTIABLE). Zadanie testowe poprzedza
implementację i MUSI najpierw padać **na asercji** (nie na kompilacji — w razie potrzeby szkielet typów
z `NotImplementedException`). Osobne commity: `test: … (red)`, potem `feat:`/`fix: …` (green). Testy
offline i deterministyczne. Zadania „treść” (pliki YAML) nie mają osobnego testu red: ich testem jest test próbki (T072),
napisany przed danymi, oraz walidacje generatora (w tym `CorpusChecks.TemplateStructure`). Każde
zadanie treści kończy się zielonym `generate --types <typ>` i zieloną próbką.

**Organization**: Zadania pogrupowane wg historyjek ze spec.md: US1 (P1) korpus bazowy jednym
poleceniem, US2 (P1) wierna konwersja i metryki, US3 (P2) wersje / nieaktualne / sprzeczne, US4 (P2)
dokumenty zatrute, US5 (P3) rozbudowa bez kodu i README, US6 (P3) akty prawne.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: można wykonać równolegle (inne pliki, brak zależności od niezakończonych zadań)
- **[Story]**: historyjka (US1…US6)

## Path Conventions

- Generator: `src/LegalAgent.Corpus/` (dalej `corpus-lib/`), CLI: `src/LegalAgent.Corpus.Cli/`, testy
  generatora: `tests/LegalAgent.Corpus.Tests/` (dalej `ctests/`), testy parsera:
  `tests/LegalAgent.PdfParser.Tests/` (dalej `ptests/`), dane: `corpus/`.
- Konwencje jak w 001/002: `CultureInfo.InvariantCulture`, `StringComparison.Ordinal*`, jawne
  sortowania (ordinal), LF, UTF-8 bez BOM, XML-doc publicznych typów, `TreatWarningsAsErrors`.
- Żadnego `DateTime.Now`, `Guid.NewGuid`, `System.Random`, `Directory.GetFiles` bez sortowania w
  generatorze (FR-101).
- Odniesienia „R*n*” = decyzje w research.md; formaty plików = contracts/*.md; pola = data-model.md.
- Treść: wyłącznie fikcyjny „Bank Przykładowy S.A.”, domeny `example.com`/`przyklad.invalid`, numery
  telefonów 800 000 000–800 000 099, KRS/NIP z samych zer lub sekwencji niemożliwych (FR-105).

---

## Phase 1: Setup

- [X] T001 Utwórz projekty i dodaj je do `LegalAgent.slnx`: `src/LegalAgent.Corpus/LegalAgent.Corpus.csproj` (class library, `GenerateDocumentationFile`, `RootNamespace` `LegalAgent.Corpus`, `PackageReference` PdfPig i YamlDotNet, `ProjectReference` do `src/LegalAgent.PdfParser`, `InternalsVisibleTo` `LegalAgent.Corpus.Tests`), `src/LegalAgent.Corpus.Cli/LegalAgent.Corpus.Cli.csproj` (Exe, `ProjectReference` do `LegalAgent.Corpus`), `tests/LegalAgent.Corpus.Tests/LegalAgent.Corpus.Tests.csproj` (jak `ptests` csproj: xunit.v3, runner, Test.Sdk, `NoWarn CA1707`, `Using Xunit`, referencje do obu nowych projektów i do parsera); `dotnet build LegalAgent.slnx` zielony
- [X] T002 [P] Dodaj `<PackageVersion Include="YamlDotNet" Version="16.3.0" />` do `Directory.Packages.props` (uzasadnienie w opisie commita: plan.md Complexity Tracking)
- [X] T003 [P] Dodaj `.gitattributes` w katalogu głównym: `corpus/**/*.md text eol=lf`, `corpus/**/*.json text eol=lf`, `corpus/**/*.yaml text eol=lf`, `corpus/**/*.pdf binary`; utwórz `corpus/` z pustymi katalogami `zrodla/szablony`, `zrodla/bloki/{wspolne,regulaminy,taryfy,procedury}`, `zrodla/zatrucia`, `akty/` (pliki `.gitkeep`)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: konstruktor PDF w bibliotece, determinizm, wczytanie treści, skład stron, prawda
referencyjna, zapis — wspólne dla wszystkich historyjek.

**⚠️ CRITICAL**: żadna historyjka nie startuje przed końcem tej fazy.

### Przeniesienie konstruktora PDF i determinizm (R1, R4)

- [X] T004 Przenieś `ptests/Fixtures/SyntheticPdfBuilder.cs` do `corpus-lib/Pdf/SyntheticPdfBuilder.cs` (namespace `LegalAgent.Corpus.Pdf`, `public sealed`, bez zmian zachowania) i czcionki `ptests/Fixtures/Fonts/*` do `corpus-lib/Fonts/` jako `EmbeddedResource` (ładowanie przez `Assembly.GetManifestResourceStream`, nie `AppContext.BaseDirectory`); przenieś `SyntheticPdfBuilderTests.cs` do `ctests/Unit/Pdf/`; w `ptests` csproj dodaj `ProjectReference` do `LegalAgent.Corpus` i `<Using Include="LegalAgent.Corpus.Pdf" />`, usuń wpis `Content Fixtures\Fonts\*`; potwierdź: pełny zestaw testów zielony, `GoldenTests` bez zmian (refaktoryzacja bez zmiany zachowania — jeden commit `refactor:`)
- [X] T005 [P] Testy w `ctests/Unit/Pdf/PdfIdNormalizerTests.cs` (red): dwa wywołania `SyntheticPdfBuilder.Build()` dla tego samego dokumentu (1 i 25 stron) dają identyczne bajty; `/ID` zachowuje długość i format `[ <32 hex><32 hex> ]`; różna treść → różne `/ID`; PDF otwiera się w PdfPig z tą samą liczbą stron; tabela xref poprawna (PdfPig nie zgłasza naprawy)
- [X] T006 Zaimplementuj `corpus-lib/Pdf/PdfIdNormalizer.cs` (R4: SHA-256 treści z wyzerowanym `/ID`, podmiana w miejscu) i wywołaj w `SyntheticPdfBuilder.Build()` — T005 green; goldeny parsera bez zmian
- [X] T007 [P] Testy w `ctests/Unit/Random/DeterministicRandomTests.cs` (red): SplitMix64 dla ziarna 0 i 20261008 daje zapisane w teście pierwsze 5 wartości (wektory referencyjne); `Derive(seed, "cel", "id")` stabilne (wartość zapisana w teście) i różne dla różnych `cel`/`id`; `Next(n)` w `[0, n)`; `Shuffle` deterministyczne (Fisher–Yates); FNV-1a 64 po UTF-8 dla „zażółć” = wartość referencyjna
- [X] T008 Zaimplementuj `corpus-lib/Random/DeterministicRandom.cs` (R3) — T007 green

### Treść źródłowa (R2, contracts/content-format.md)

- [X] T009 [P] Testy w `ctests/Unit/Content/TextTemplateTests.cs` (red): `{a|b|c}` wybiera wariant ziarnem (ten sam seed → ten sam wybór, rozkład pokrywa wszystkie warianty na 300 próbach); zagnieżdżenie `{x {a|b}|y}`; `\{`, `\}`, `\|` dosłowne; `{{fakt:id}}` formatowane wg rodzaju (`kwota` 25 → „25,00 zł”, 1234.5 → „1 234,50 zł” z twardą spacją U+00A0 zamienioną na zwykłą spację, `procent` 1.5 → „1,5%”, `termin` 14 → „14 dni”, `data` → „1 stycznia 2027 r.”); `{{param:bank}}` → „Bank Przykładowy S.A.”; `**x**`/`*x*` → inline pogrubienie/kursywa; `[^1]` → odwołanie do przypisu; nieznany fakt/parametr i niezamknięty nawias → `ContentException` z pozycją; `{{ref:…}}` zwraca symbol odroczony rozwiązywany po numeracji
- [X] T010 Zaimplementuj `corpus-lib/Content/TextTemplate.cs` (parser + renderer do `Inline[]`) i `corpus-lib/Content/PolishFormat.cs` (kwoty, procenty, terminy z odmianą „1 dzień / 2 dni / 5 dni”, daty słownie) — T009 green
- [X] T011 [P] Testy w `ctests/Unit/Content/ContentLoaderTests.cs` (red) na plikach w `ctests/TestData/zrodla-mini/` (1 fakt każdego rodzaju, 1 szablon na typ, 6 bloków, 1 wzorzec zatrucia, `zabronione.yaml`, `akty.yaml`): wczytanie do `ContentLibrary`; nieznany klucz, duplikat `id`, blok bez `typy`, wzorzec `polecenia-dla-ai` bez `cel`, szablon ze stylem układu spoza listy (`jedna-kolumna`, `dwie-kolumny`, `tabela-dokument`, `taryfa-siatka`, `taryfa-bez-siatki`, `procedura`) → `ContentException` z nazwą pliku i ścieżką YAML; pliki wczytywane w porządku ordinal; `ContentHash` stabilny (SHA-256 po ordinal ścieżkach i bajtach); `typy.yaml` — duplikat `Id`/`Prefix`, szablon z typem spoza `typy.yaml` → `ContentException`
- [X] T012 Zaimplementuj modele `corpus-lib/Content/{ContentLibrary,Fact,FactCatalog,DocumentTemplate,ContentBlock,ContentElement,PoisonPattern,ActSource}.cs` (data-model.md §1, wszystkie pola i reguły; `Fact.Values` uporządkowane po dacie, „pierwsza bez daty = wartość bazowa”) i `corpus-lib/Content/ContentLoader.cs` (YamlDotNet, `IgnoreUnmatchedProperties` WYŁĄCZONE) — T011 green
- [X] T013 [P] Testy w `ctests/Unit/Content/FactCatalogTests.cs` (red): wartość faktu na dzień (przed/po `od`), nadpisanie dokumentu ma pierwszeństwo, `Alternatives` różne od wszystkich `Values` (walidacja przy wczytaniu), brak faktu → wyjątek z id
- [X] T014 Zaimplementuj `FactCatalog.ValueAt(id, date, overrides)` w `corpus-lib/Content/FactCatalog.cs` — T013 green

### Skład stron i prawda referencyjna (R6, R7, FR-106)

- [X] T015 [P] Testy w `ctests/Unit/Typesetting/TypesetterTests.cs` (red), każdy sprawdza PDF przez PdfPig i `DocumentTruth`: akapit łamany po szerokości kolumny (żadna linia nie przekracza prawego marginesu, słowa w kolejności = `Truth.Words`); nagłówek rozdziału i „§ N.” → `Truth.Headings` z poziomem; lista 3-poziomowa „1.”/„1)”/„a)” z wcięciami → `Truth.ListItems` (label, depth); przypis: znacznik w tekście, treść u dołu tej samej strony nad stopką; nagłówek i stopka strony + „Strona n z N” (dwa przebiegi) → `Truth.Artifacts`, nieobecne w `Truth.Words`; przejście akapitu przez stronę bez gubienia słów; `Image` (strzałka) i prostokąt (pole wyboru) nie dają słów
- [X] T016 Zaimplementuj `corpus-lib/Typesetting/Typesetter.cs` (uogólniony `Flow` z `ptests/Fixtures/BankingCorpusGenerator.cs`: kolumny, `Line`, `Item`, przypisy na stronie, `OnPageEnd`, dwa przebiegi numeracji) i `corpus-lib/Truth/DocumentTruth.cs` (data-model.md §3: `Words`, `Headings(Level, Label, Text)`, `ListItems(Label, Depth, FirstWords)`, `Tables`, `Artifacts`, `PoisonTexts`) oraz `TypesetResult` (`Pdf`, `PageCount`, `Truth`, `ElementPages`, `BlockWordCounts`) — T015 green
- [X] T017 [P] Testy w `ctests/Unit/Typesetting/TablesTests.cs` (red): tabela z siatką przez 3 strony z powtarzanym wierszem nagłówka (powtórzenia nie w `Truth.Tables`, ale w PDF obecne), tabela bez siatki (kolumny wyrównane, brak linii), komórka wieloliniowa, znacznik przypisu „1)” w komórce i treść przypisu pod tabelą, wiersz nie dzielony przez granicę strony; tabela klucz–wartość (metryczka) z siatką
- [X] T018 Zaimplementuj tabele w `corpus-lib/Typesetting/TableLayout.cs` (używane przez `Typesetter`) — T017 green
- [X] T019 [P] Testy w `ctests/Unit/Typesetting/SpecialLayoutsTests.cs` (red): dwie kolumny (lewa wypełniona przed prawą, `Truth.Words` w kolejności lewa→prawa, nagłówki „§ N.” w obu kolumnach); tabela-dokument zgodna z FR-080 (ramka x 54/181/541, poziome linie w dwóch kawałkach, wiersz nazw kolumn na stronach, komórka przez granicę strony z pustą lewą komórką; nazwy sekcji → `Truth.Headings`); schemat kroków zgodny z FR-067 (szare pola ≤ 50% szerokości z nazwą kroku, wyjaśnienie po prawej, strzałki-obrazy, wiersz nazw kolumn; nazwy kroków w `Truth` jako pogrubione akapity, nie nagłówki); lista kontrolna w 3 formach (wektorowe pole + tekst, „□” krojem mono, tabela „Lp. | Czynność | Wykonano” z pustymi polami); ramka (`Callout`) z obramowaniem
- [X] T020 Zaimplementuj `corpus-lib/Typesetting/{TwoColumnLayout,TableDocumentLayout,StepSchemeLayout,ChecklistLayout}.cs` — T019 green
- [X] T021 [P] Testy w `ctests/Unit/Typesetting/LayoutStylesTests.cs` (red): 6 stylów (`jedna-kolumna`, `dwie-kolumny`, `tabela-dokument`, `taryfa-siatka`, `taryfa-bez-siatki`, `procedura`) — każdy z okładką i/lub metryczką, nagłówkiem i stopką strony, numeracją; style różnią się marginesami/krojami/rozmiarami; ten sam dokument w tym samym stylu = identyczne bajty
- [X] T022 Zaimplementuj `corpus-lib/Typesetting/LayoutStyles.cs` (okładka: nazwa banku, tytuł, oznaczenie, wersja, daty; metryczka procedury: oznaczenie, wersja, właściciel, zatwierdził, data zatwierdzenia, daty obowiązywania, historia zmian) — T021 green

### Zapis i walidacja przekrojowa (R14, FR-105, FR-108)

- [X] T023 [P] Testy w `ctests/Unit/Output/CorpusWriterTests.cs` (red, katalogi tymczasowe): zapis atomowy (plik tymczasowy + przeniesienie; przerwanie przed przeniesieniem nie zostawia uszkodzonego pliku docelowego); UTF-8 bez BOM, `\n`, końcowe `\n`; sprzątanie usuwa `*.pdf`/`*.md` w `regulaminy/`, `taryfy/`, `procedury/`, `zatrute/**` nieobecne w planie, nie dotyka `README.md`, `przebieg.json`, `zrodla/`, `akty/*.pdf`, plików innych rozszerzeń; sprzątanie tylko po udanym przebiegu; `Compare` (dla `verify`) zwraca listę różniących się / brakujących / nadmiarowych plików
- [X] T024 Zaimplementuj `corpus-lib/Output/CorpusWriter.cs` — T023 green
- [X] T025 [P] Testy w `ctests/Unit/Validation/CorpusChecksTests.cs` (red): nazwa zabroniona wykryta bez względu na wielkość liter i diakrytyki („ŻÓŁTY bank” vs „zolty bank”), wynik wskazuje id bloku źródłowego; unikalność bloków niewspólnych po znormalizowanym tekście (małe litery, pojedyncze spacje) między dokumentami bazowymi; udział słów bloków wspólnych > 20% → błąd z wartością; odwołanie `{{ref:…}}` do nieistniejącej jednostki → błąd z dokumentem i blokiem; szablon typu bez elementu z `wymagane-elementy` (`CorpusChecks.TemplateStructure`) → błąd z nazwą szablonu i brakującym elementem
- [X] T026 Zaimplementuj `corpus-lib/Validation/CorpusChecks.cs` — T025 green

**Checkpoint**: biblioteka składa pojedyncze dokumenty z elementów w 6 stylach z prawdą referencyjną,
deterministycznie; testy parsera bez zmian.

---

## Phase 3: User Story 1 — Korpus bazowy wygenerowany jednym poleceniem (Priority: P1) 🎯 MVP

**Goal**: `generate` z zapisanymi parametrami tworzy po 10 regulaminów, taryf i procedur (20–30 stron)
z PDF, Markdown i manifestem; drugie uruchomienie nie zmienia bajtów.

**Independent Test**: quickstart.md §3 — `generate` w pustym katalogu, liczby dokumentów i stron,
`verify` = 0, drugi `generate` bez zmian w `git status`.

### Tests for User Story 1 ⚠️

- [X] T027 [P] [US1] Testy w `ctests/Unit/Planning/RunParametersTests.cs` (red): odczyt/zapis `przebieg.json` (pola i domyślne z data-model.md §2: `Seed` 20261008, `ReferenceDate` 2026-10-01, `DocumentsPerType` 10 „1–500”, `Pages` 20–30 „1 ≤ Min ≤ Max ≤ 500”, `VersionedShare` 30 „0–100; liczba = zaokrąglenie w górę”, `MaxVersions` 3 „2–5”, `OutdatedPerType` 2, `ContradictionPairsPerType` 1, `CrossTypeContradictionPairs` 1, `StrictUniqueness` true, `MaxSharedShare` 20, `ParserOptions` = domyślne `PdfParserOptions` z zapisem/odczytem); wartości spoza zakresu → błąd walidacji z nazwą pola; ścieżki zapisywane względnie z `/`
- [X] T028 [P] [US1] Testy w `ctests/Unit/Planning/CorpusPlannerTests.cs` (red, `zrodla-mini` rozszerzone o 3 szablony na typ): plan bazowy — `DocumentsPerType` dokumentów na typ, id `REG-01`…, oznaczenia `BP/REG/01` unikalne, szablony przydzielone round-robin po tasowaniu ziarnem, styl układu z listy szablonu (co najmniej 2 regulaminy `dwie-kolumny` i 2 `tabela-dokument`, co najmniej 2 taryfy `taryfa-siatka` i 2 `taryfa-bez-siatki` — FR-110/FR-111; gdy szablony na to nie pozwalają → błąd planowania); pule bloków niewspólnych rozłączne między dokumentami (R3); `TargetPages` w zakresie; ten sam seed → identyczny plan (porównanie serializacji); zmiana seed → inny plan; plan dokumentu `REG-03` nie zależy od tego, czy generowane są inne typy
- [X] T029 [P] [US1] Testy w `ctests/Unit/Composition/DocumentComposerTests.cs` (red): plan → drzewo elementów; numeracja „§ N.” ciągła w dokumencie, kroki „N.”, „N.N.”, „N.N.N.”; `{{ref:blok:…}}` rozwiązane do „§ 14”; fakty z datą początku obowiązywania; blok niewspólny co najwyżej raz w dokumencie; czoło dokumentu z polami FR-113 (bank, tytuł, oznaczenie, wersja, data od, data do jeśli dotyczy)
- [X] T030 [P] [US1] Testy w `ctests/Unit/Typesetting/PageFitterTests.cs` (red): dobór bloków opcjonalnych trafia w `TargetPages` z tolerancją zakresu; za mało bloków → `CorpusGenerationException` „nieosiągalny zakres stron” z dokumentem i szablonem; maks. 8 iteracji; wynik deterministyczny
- [X] T031 [P] [US1] Testy w `ctests/Unit/Manifest/ManifestWriterTests.cs` (red): JSON wg contracts/manifest.md — `schemaVersion` 1, `run` (seed, referenceDate, parameters, parserVersion, generatorVersion, contentHash, bez znacznika czasu), wpisy z polami wspólnymi (`id`, `type`, `title`, `designation`, `version`, `validFrom`, `validTo`, `status`, `pdf`, `markdown`, `pages`, `template`, `layout`, `seed`, `sharedWordShare`), stała kolejność kluczy, pomijane `null`, kolejność wpisów: akty, regulaminy, taryfy, procedury, zatrute (ordinal id); wcięcie 2 spacje; brak ról/uprawnień
- [X] T032 [P] [US1] Testy w `ctests/Unit/CorpusGeneratorTests.cs` (red, `zrodla-mini`, katalog tymczasowy, 2 dokumenty na typ, 2–3 strony): `Generate` zapisuje PDF+MD w `regulaminy/`, `taryfy/`, `procedury/` wg contracts/corpus-layout.md i `manifest.json`; drugi `Generate` → identyczne bajty wszystkich plików; `Verify` po `Generate` → brak różnic; zmiana jednego bajtu MD → `Verify` zgłasza plik; Markdown = wynik `PdfMarkdownConverter.CreateDefault()` z `SourceId` = ścieżka względna; konwersja niekompletna → błąd (kod 5)
- [X] T033 [P] [US1] Testy w `ctests/Cli/ProgramTests.cs` (red): `generate --params <plik> --out <tmp>` → kod 0; `--help`, `--version`; błędny parametr → kod 2 i komunikat po polsku na stderr; błąd w YAML → kod 2 z plikiem; nieosiągalny zakres stron → kod 3; nazwa zabroniona → kod 4; `verify` z różnicą → kod 1 (contracts/cli.md „Kody wyjścia”)

### Implementation for User Story 1

- [X] T034 [US1] Zaimplementuj `corpus-lib/Planning/RunParameters.cs` — T027 green
- [X] T035 [US1] Zaimplementuj `corpus-lib/Planning/{CorpusPlanner,DocumentPlan}.cs` dla dokumentów bazowych (bez wersji, sprzeczności i zatruć) — T028 green
- [X] T036 [US1] Zaimplementuj `corpus-lib/Composition/{DocumentComposer,Elements}.cs` — T029 green
- [X] T037 [US1] Zaimplementuj `corpus-lib/Typesetting/PageFitter.cs` — T030 green
- [X] T038 [US1] Zaimplementuj `corpus-lib/Manifest/{Manifest,ManifestWriter}.cs` (pola wspólne; `changes`/`contradictions`/`poison`/`source` jako puste typy na później) — T031 green
- [X] T039 [US1] Zaimplementuj `corpus-lib/Conversion/MarkdownRefresher.cs` (R13) i fasadę `corpus-lib/CorpusGenerator.cs` (`Generate`, `Verify`; konwersja z `RunParameters.ParserOptions`; składanie dokumentów równolegle z deterministycznym porządkiem wyniku) — T032 green
- [X] T040 [US1] Zaimplementuj `src/LegalAgent.Corpus.Cli/Program.cs` (polecenia `generate`, `verify`, opcje `--params`, `--out`, `--content`, `--seed`, `--truth`; kody wyjścia 0–6) — T033 green

### Treść korpusu bazowego (US1)

Każde zadanie treści: szablon `corpus/zrodla/szablony/<id>.yaml` + bloki `corpus/zrodla/bloki/<typ>/<temat>.yaml`
z wariantami i odwołaniami do faktów; dokument MUSI dać się wygenerować w zakresie 20–30 stron przy
`StrictUniqueness` (`generate --types <typ> --out <tmp>`), bez nazw zabronionych, z treścią merytorycznie
spójną z tematem (FR-114). Teksty pisane od nowa (bez kopiowania regulaminów prawdziwych banków).

Elementy obowiązkowe każdego szablonu (pole `wymagane-elementy` typu w `typy.yaml`, sprawdzane przez
`CorpusChecks.TemplateStructure`):
- **regulaminy (FR-110)**: okładka; rozdziały; „§ N.” z ustępami „1.” i punktami „1)” (≥ 1 paragraf z literami „a)”); słowniczek definicji; ≥ 3 przypisy; odwołania do taryfy i do ≥ 1 aktu prawnego.
- **taryfy (FR-111)**: okładka; ≥ 2 sekcje segmentów klientów; tabela przez ≥ 3 strony z powtarzanym wierszem nagłówka; numerowane pozycje („1.”, „1.1.”); ≥ 5 przypisów do pozycji (znacznik w komórce, treść pod tabelą/na końcu sekcji).
- **procedury (FR-112)**: metryczka (oznaczenie, wersja, właściciel, zatwierdził, data zatwierdzenia, daty obowiązywania, historia zmian); sekcje: cel, zakres, odpowiedzialności, definicje, opis postępowania; kroki „N.”/„N.N.”/„N.N.N.”; ≥ 1 schemat kroków (FR-067); ≥ 1 lista kontrolna; ≥ 1 załącznik.

- [X] T041 [US1] Treść wspólna: `corpus/zrodla/typy.yaml` (3 typy z prefiksami `REG`, `TAR`, `PRO` i `wymagane-elementy` jak wyżej), `corpus/zrodla/fakty.yaml` (opłaty, oprocentowania, limity, terminy reklamacji 15/30/60 dni, jednostki organizacyjne — Departament Zgodności, Departament Operacji, Biuro Reklamacji, Departament Bezpieczeństwa, Zarząd — adresy, infolinia, adres korespondencyjny, wszystkie fikcyjne), `corpus/zrodla/zabronione.yaml` (nazwy i znaki towarowe banków działających w Polsce i ich marek), `corpus/zrodla/bloki/wspolne/*.yaml` (bloki `wspolny: true`: reklamacje, ochrona danych osobowych, zmiany dokumentu, kontakt z bankiem, doręczenia, prawo właściwe, Rzecznik Finansowy, BFG)
- [X] T042 [P] [US1] Regulamin rachunku osobistego (`regulamin-rachunku-osobistego`, styl `jedna-kolumna`)
- [X] T043 [P] [US1] Regulamin rachunków oszczędnościowych i lokat (`regulamin-lokat`, `jedna-kolumna`)
- [X] T044 [P] [US1] Regulamin kart debetowych (`regulamin-kart-debetowych`, `dwie-kolumny`)
- [X] T045 [P] [US1] Regulamin kart kredytowych (`regulamin-kart-kredytowych`, `dwie-kolumny`)
- [X] T046 [P] [US1] Regulamin kredytu gotówkowego (`regulamin-kredytu-gotowkowego`, `jedna-kolumna`)
- [X] T047 [P] [US1] Regulamin kredytu hipotecznego (`regulamin-kredytu-hipotecznego`, `jedna-kolumna`)
- [X] T048 [P] [US1] Regulamin bankowości elektronicznej (`regulamin-bankowosci-elektronicznej`, `jedna-kolumna` lub `dwie-kolumny`)
- [X] T049 [P] [US1] Regulamin promocji „konto z premią” (`regulamin-promocji-konto`, `tabela-dokument`)
- [X] T050 [P] [US1] Regulamin promocji kart (`regulamin-promocji-karty`, `tabela-dokument`)
- [X] T051 [P] [US1] Regulamin rachunku firmowego (`regulamin-rachunku-firmowego`, `jedna-kolumna`)
- [X] T052 [P] [US1] Taryfa — klienci indywidualni (`taryfa-indywidualni`, `taryfa-siatka`, sekcje segmentów)
- [X] T053 [P] [US1] Taryfa — firmy (`taryfa-firmy`, `taryfa-bez-siatki`)
- [X] T054 [P] [US1] Taryfa — bankowość prywatna (`taryfa-bankowosc-prywatna`, `taryfa-siatka`)
- [X] T055 [P] [US1] Taryfa — karty płatnicze (`taryfa-karty`, `taryfa-bez-siatki`)
- [X] T056 [P] [US1] Taryfa — kredyty i pożyczki (`taryfa-kredyty`, `taryfa-siatka`)
- [X] T057 [P] [US1] Taryfa — przelewy zagraniczne i wymiana walut (`taryfa-zagraniczne`, `taryfa-bez-siatki`)
- [X] T058 [P] [US1] Taryfa — rachunki oszczędnościowe i lokaty (`taryfa-oszczednosci`, `taryfa-siatka`)
- [X] T059 [P] [US1] Taryfa — usługi kasowe i skrytki (`taryfa-uslugi-kasowe`, `taryfa-bez-siatki`)
- [X] T060 [P] [US1] Taryfa — bankowość elektroniczna (`taryfa-bankowosc-elektroniczna`, `taryfa-siatka`)
- [X] T061 [P] [US1] Taryfa — młodzież i studenci (`taryfa-mlodziez`, `taryfa-bez-siatki`)
- [X] T062 [P] [US1] Procedura otwierania rachunku (`procedura-otwarcie-rachunku`, `procedura`)
- [X] T063 [P] [US1] Procedura rozpatrywania reklamacji (`procedura-reklamacje`)
- [X] T064 [P] [US1] Procedura AML/KYC (`procedura-aml-kyc`)
- [X] T065 [P] [US1] Procedura blokad i zajęć egzekucyjnych (`procedura-blokady`)
- [X] T066 [P] [US1] Procedura obsługi zgonu klienta (`procedura-zgon-klienta`)
- [X] T067 [P] [US1] Procedura obsługi incydentów bezpieczeństwa (`procedura-incydenty`)
- [X] T068 [P] [US1] Procedura zastrzegania kart i transakcji nieautoryzowanych (`procedura-zastrzezenia`)
- [X] T069 [P] [US1] Procedura pełnomocnictw (`procedura-pelnomocnictwa`)
- [X] T070 [P] [US1] Procedura zamknięcia rachunku (`procedura-zamkniecie-rachunku`)
- [X] T071 [P] [US1] Procedura realizacji praw osób, których dane dotyczą (`procedura-rodo`)

### Przebieg korpusu (US1)

- [X] T072 [US1] Testy próbki w `ctests/Corpus/CorpusSampleTests.cs` (red): wybór automatyczny z `corpus/przebieg.json` (R10: pierwszy dokument dla każdej pary typ × styl układu); dla każdego: PDF odtworzony w pamięci == plik w `corpus/`, Markdown z biblioteki == plik, liczba stron w zakresie, wpis manifestu z polami FR-140. Zapisz `corpus/przebieg.json` (parametry domyślne, `Poison` puste do US4, wersje/sprzeczności 0 do US3); test pada na asercji „brak pliku corpus/regulaminy/REG-01.pdf”
- [X] T073 [US1] Uruchom `generate` wg `corpus/przebieg.json` i zacommituj `corpus/{regulaminy,taryfy,procedury}/*` i `manifest.json` (commit `data:`) — T072 green; `verify` = 0; drugi `generate` bez zmian w `git status`
- [X] T072a [US1] Dodaj do `.github/workflows/ci.yml` krok po testach: `dotnet run --project src/LegalAgent.Corpus.Cli -c Release --no-build -- verify` (kod ≠ 0 = błąd CI); wypchnij gałąź i potwierdź, że `verify` na Ubuntu przechodzi na korpusie wygenerowanym na Windows (FR-101, SC-021). Różnice bajtów → diagnoza (np. formatowanie liczb w PdfPig) i poprawka w `corpus-lib/Pdf/` test-first przed dalszymi fazami

**Checkpoint**: MVP — korpus 10 × 3 w repozytorium, odtwarzalny jednym poleceniem.

---

## Phase 4: User Story 2 — Wierna, cytowalna konwersja całego korpusu (Priority: P1)

**Goal**: metryki jakości względem prawdy referencyjnej dla każdego dokumentu; układy źle obsłużone
przez bibliotekę poprawione test-first; Markdown korpusu odświeżony.

**Independent Test**: `LEGALAGENT_CORPUS_FULL=1 dotnet test --filter Category=CorpusFull` — SC-022 – SC-026
dla wszystkich dokumentów (quickstart.md §2).

### Tests for User Story 2 ⚠️

- [X] T074 [P] [US2] Testy w `ctests/Unit/QualityMetricsTests.cs` (red) dla `ctests/Corpus/QualityMetrics.cs` na małych `DocumentTruth` + Markdown/model: kompletność słów (z odescapowaniem Markdown i usunięciem `<!-- page: N -->`; słowo spoza PDF wykryte), kolejność czytania, nagłówki (poziom i oryginalne oznaczenie; fałszywy nagłówek liczony), pozycje list (label, głębokość), tabele (stawka w wierszu nazwy usługi i numeru pozycji; jedna tabela na tabelę źródłową; brak powtórzonych nagłówków; zgodność komórek), raport wskazuje dokument i jednostkę przy niepowodzeniu
- [X] T075 [US2] Zaimplementuj `ctests/Corpus/QualityMetrics.cs` — T074 green
- [X] T076 [US2] Rozszerz `ctests/Corpus/CorpusSampleTests.cs` o metryki SC-022 (≥ 99,5% słów, 0 słów spoza PDF), SC-024 (≥ 98% nagłówków, ≤ 1% fałszywych), SC-025 (≥ 98% list), SC-026 (100% stawek w wierszu, 100% tabel taryf jako GFM bez powtórzonych nagłówków, ≥ 98% komórek); utwórz `ctests/Corpus/CorpusFullTests.cs` (`[Trait("Category","CorpusFull")]`, `Skip` gdy brak `LEGALAGENT_CORPUS_FULL`) z tymi samymi asercjami dla wszystkich dokumentów + SC-020; uruchom pełny zestaw i zapisz wyniki pomiaru (odsetki per dokument i lista niepowodzeń) w `specs/003-synthetic-bank-corpus/research.md` „Pomiar korpusu” — niepowodzenia to czerwone stany dla T077–T088

### Implementation for User Story 2 (poprawki biblioteki — tylko potwierdzone pomiarem T076)

Każda para: czerwony test na minimalnym PDF z `SyntheticPdfBuilder` w `ptests/Integration/CorpusLayoutsIntegrationTests.cs`
(lub test etapu w `ptests/Unit/Stages/…`) → poprawka w `src/LegalAgent.PdfParser/` → goldeny parsera
bez zmian (inaczej zgoda właściciela, FR-163). Zadania niepotwierdzone pomiarem oznaczyć `[X]` z
adnotacją „nie dotyczy — pomiar T076”.

- [X] T077 [US2] Test (red): metryczka procedury (tabela klucz–wartość z siatką, 8 wierszy, 1. strona) → tabela GFM 2-kolumnowa, nie tabela-dokument ani nagłówki (R11) — nie dotyczy — pomiar T076 (metryczka jest tabelą GFM 2-kolumnową)
- [X] T078 [US2] Poprawka dla T077 w `src/LegalAgent.PdfParser/Stages/` (green) — nie dotyczy — pomiar T076
- [X] T079 [US2] Test (red): kroki „4.1.”, „4.1.1.” po nagłówku „4. Opis postępowania” (krok bywa pogrubiony) → pozycje list z oryginalnym oznaczeniem i poziomem z hierarchii, nie nagłówki (R11, FR-161)
- [X] T080 [US2] Poprawka dla T079 (green)
- [X] T081 [US2] Test (red): lista kontrolna z polem wektorowym i w tabeli „Lp. | Czynność | Wykonano” → pozycje/wiersze z pełnym tekstem
- [X] T082 [US2] Poprawka dla T081 (green)
- [X] T083 [US2] Test (red): taryfa bez siatki przez 3 strony z powtarzanym nagłówkiem i przypisami „1)” pod tabelą → jedna tabela GFM, przypisy jako akapity/przypisy po tabeli, znacznik przypisu w komórce stawki
- [X] T084 [US2] Poprawka dla T083 (green)
- [X] T085 [US2] Test (red): akapit zawierający „# SYSTEM:”, „> polecenie”, „§ 99.” w środku i na początku linii łamania → tekst dosłowny w akapicie, bez nagłówka/cytatu/jednostki (FR-162)
- [X] T086 [US2] Poprawka dla T085 w `src/LegalAgent.PdfParser/Rendering/MarkdownEscaper.cs` lub etapie nagłówków (green)
- [X] T087 [US2] Test (red): dwie kolumny z przypisami i „§ N.” w obu kolumnach → kolejność lewa→prawa, nagłówki w kolejności, przypisy kompletne — `CorpusLayoutsIntegrationTests` T087–T087j
- [X] T088 [US2] Poprawka dla T087 (green) — poprawki T087–T087j w `LineAssemblyStage`, `ReadingOrderStage`, `TableDetectionStage`, `ListDetectionStage`, `FootnoteDetectionStage`
- [X] T089 [US2] Inne niepowodzenia z pomiaru T076 — każde jako para red/green dopisana tutaj (T089a, T089b, …) przed implementacją; zachowanie sprzeczne ze spec → doprecyzowanie FR w spec.md w commicie red — T089a–T089c; pomiar w research.md „Pomiar korpusu (T076)”
- [X] T089a [US2] Test (red): tabela z siatką bezpośrednio pod akapitami numerowanymi „1.” z wcięciem wiszącym → lista zachowana, tabela GFM bez wierszy spoza siatki (`ptests/Integration/CorpusLayoutsIntegrationTests.cs`); poprawka w `TableDetectionStage` (green) — wiersze nad górną linią siatki nie należą do tabeli z siatką
- [X] T089c [US2] Taryfy bez siatki (pomiar T076, T083b–T083i w `CorpusLayoutsIntegrationTests`): wiersz zawinięty w dwóch kolumnach, akapit nad pogrubionym nagłówkiem kolumn, tekst między dwiema tabelami, kontynuacja na kolejnej stronie (także z podpozycjami), wiersz o prawie równych odstępach, nagłówek z jednym wierszem na dole strony i kontynuacja z jednym wierszem na górze strony — poprawki w `TableDetectionStage`
- [X] T089b [US2] Test (red): tytuł rozdziału zawinięty w dwa wiersze pod „Rozdział 6” (REG-06) → jeden nagłówek z całym tytułem; poprawka w `HeadingDetectionStage` (green)
- [X] T090 [US2] Dodaj polecenie `refresh` (konwersja istniejących PDF bez składania, przepisanie manifestu) w `corpus-lib/CorpusGenerator.cs` i `src/LegalAgent.Corpus.Cli/Program.cs` z testem w `ctests/Cli/ProgramTests.cs` (red → green); uruchom `refresh`, przejrzyj diff `corpus/**/*.md`, zacommituj; pełny zestaw `CorpusFull` zielony

**Checkpoint**: SC-022 – SC-026 spełnione na całym korpusie; goldeny parsera bez niezatwierdzonych zmian.

---

## Phase 5: User Story 3 — Wersje, dokumenty nieaktualne i sprzeczne (Priority: P2)

**Goal**: ≥ 3 dokumenty na typ w 2–3 wersjach, ≥ 2 nieaktualne na typ, pary sprzeczne (1 na typ + 1
między typami), wszystko w treści i w manifeście.

**Independent Test**: quickstart.md §4 pkt 1 + testy US3 na korpusie.

### Tests for User Story 3 ⚠️

- [X] T091 [P] [US3] Testy w `ctests/Unit/Planning/VersionsPlannerTests.cs` (red): `VersionedShare` 30% z 10 → 3 dokumenty (zaokrąglenie w górę), liczba wersji 2–`MaxVersions`; wersje: to samo oznaczenie i tytuł, kolejne numery, „`ValidTo(n) + 1 dzień = ValidFrom(n+1)`”, wcześniejsze `Status = Nieaktualny`, `PreviousVersionId`; co najmniej jedna zmiana (nadpisanie faktu z `Values` z datą lub inny wariant bloku), reszta treści identyczna; dokumenty nieaktualne (`OutdatedPerType`, „`ValidTo < ReferenceDate`”, bez następcy, rozłączne z wersjonowanymi); pary sprzeczne: nachodzące okresy, ten sam fakt z różnymi wartościami (`Alternatives`), para między typami regulamin–taryfa dzieli fakt; wszystko deterministyczne
- [X] T092 [P] [US3] Testy w `ctests/Unit/Manifest/ManifestChangesTests.cs` (red): `changes[]` (`unit`, `page`, `fact`, `before`, `after`) i `contradictions[]` (`with`, `unit`, `page`, `fact`, `this`, `other`) wyliczone z nadpisań i `ElementPages`; jednostka w formacie „§ 12 ust. 3 pkt 2” / „poz. 4.7” / „krok 5.2”; dokument nie zawiera meta-uwag o sprzeczności (FR-123: tekst PDF nie zawiera słów „sprzeczn”, „nieaktualn” poza treścią z bloków)
- [ ] T093 [P] [US3] Testy w `ctests/Unit/CorpusGeneratorVersionsTests.cs` (red): pliki wersji `REG-03-w1.pdf`, `REG-03-w2.pdf`, najnowsza `REG-03.pdf` (contracts/corpus-layout.md); okładki z numerem wersji i datami; manifest `previousVersion`

### Implementation for User Story 3

- [X] T094 [US3] Rozszerz `corpus-lib/Planning/CorpusPlanner.cs` o wersje, nieaktualne i pary sprzeczne — T091 green
- [X] T095 [US3] Rozszerz `corpus-lib/Manifest/ManifestWriter.cs` i `corpus-lib/Composition/DocumentComposer.cs` (śledzenie jednostek zmienionych faktów) — T092 green
- [ ] T096 [US3] Rozszerz `corpus-lib/CorpusGenerator.cs` i `CorpusWriter` o nazwy plików wersji — T093 green
- [ ] T097 [US3] Treść: historia wartości i `alternatywy` w `corpus/zrodla/fakty.yaml` dla faktów używanych przez co najmniej 3 dokumenty każdego typu; warianty bloków „po zmianie” (np. nowe brzmienie postanowienia) w odpowiednich plikach `bloki/`
- [ ] T098 [US3] Ustaw w `corpus/przebieg.json` wersje/nieaktualne/sprzeczności wg domyślnych, `generate`, przejrzyj i zacommituj korpus; rozszerz `CorpusFullTests` o SC-028 (wersje rozłączne i ciągłe) i spójność odwołań manifestu (`previousVersion`, `with` istnieją); próbka (T072) obejmuje wszystkie wersje pierwszego wersjonowanego dokumentu

**Checkpoint**: aplikacja RAG ma wersje, dokumenty nieaktualne i sprzeczne z prawdą w manifeście.

---

## Phase 6: User Story 4 — Dokumenty zatrute z prawdą referencyjną (Priority: P2)

**Goal**: ≥ 2 dokumenty zatrute na parę typ × rodzaj (5 rodzajów × 3 typy) w
`corpus/zatrute/<typ>/<rodzaj>/`, nieodróżnialne formą, z miejscami zatruć w manifeście.

**Independent Test**: quickstart.md §4 pkt 2; SC-023, SC-028.

### Tests for User Story 4 ⚠️

- [ ] T099 [P] [US4] Testy w `ctests/Unit/Planning/PoisonPlannerTests.cs` (red): dla każdej pary typ × rodzaj z `Poison` — `PerType` dokumentów; `ImitatesId` istnieje i ma ten sam typ; plan zatrutego = kopia planu podrabianego (szablon, styl, pule, `TargetPages`) + operacja wzorca; rodzaj bez wzorców dla typu → błąd parametrów; `polecenia-dla-ai`: każdy cel z FR-132a („`zmiana-odpowiedzi`, `ignorowanie-zrodel`, `ukrycie-zrodla`, `dzialanie-poza-zakresem`, `podszycie-pod-polecenie`”) co najmniej raz w przebiegu; na typ co najmniej jeden wariant ukryty w przypisie lub tabeli i jeden w metryczce lub na okładce (FR-132)
- [ ] T100 [P] [US4] Testy w `ctests/Unit/Composition/PoisonOperationsTests.cs` (red): `wstaw` w akapit / przypis / komórkę tabeli / metryczkę / okładkę / ramkę — styl tekstu = styl elementu docelowego (poza `ramka`), tekst widoczny (kolor czarny, rozmiar ≥ 7 pt, wewnątrz strony — FR-133); `nadpisz-fakt` zmienia wartość tylko w zatrutym; `zmien-czolo` (zatwierdził, jednostka); `przesun-daty` (okładka „obowiązuje”, choć okres minął; manifest `status: nieaktualny`); `Truth.PoisonTexts` i `ElementPages` dla każdego miejsca; liczba stron w zakresie typu
- [ ] T101 [P] [US4] Testy w `ctests/Unit/Manifest/ManifestPoisonTests.cs` (red): `poison` = `kind`, `imitates`, `description`, `places[]` (`page`, `unit`, `element` z listy contracts/manifest.md, `text` dosłowny, `goal` dla `polecenia-dla-ai`); ścieżki `zatrute/<typ>/<rodzaj>/ZAT-<PREFIKS>-<SKRÓT>-NN.pdf`

### Implementation for User Story 4

- [ ] T102 [US4] Rozszerz `corpus-lib/Planning/CorpusPlanner.cs` o plany zatrute — T099 green
- [ ] T103 [US4] Zaimplementuj `corpus-lib/Composition/PoisonOperations.cs` i rejestrację stron w `Typesetter` — T100 green
- [ ] T104 [US4] Rozszerz manifest i zapis plików o zatrute — T101 green
- [ ] T105 [P] [US4] Treść: `corpus/zrodla/zatrucia/falszywe-stawki.yaml` (wzorce dla 3 typów, operacja `nadpisz-fakt` i `wstaw` warunków)
- [ ] T106 [P] [US4] Treść: `corpus/zrodla/zatrucia/polecenia-dla-ai.yaml` (wszystkie 5 celów; warianty jawne — ramka/akapit — i ukryte — akapit, przypis, komórka tabeli, metryczka, okładka; dla każdego typu; dane fikcyjne, w tym teksty ze znakami `#`, `|`, `*`, `>`)
- [ ] T107 [P] [US4] Treść: `corpus/zrodla/zatrucia/podszywanie.yaml` (inna jednostka banku, fałszywe zatwierdzenie w metryczce, fałszywe pismo „Zarządu” w ramce)
- [ ] T108 [P] [US4] Treść: `corpus/zrodla/zatrucia/nieaktualny-jako-obowiazujacy.yaml` (`przesun-daty`, okładka/metryczka twierdząca obowiązywanie)
- [ ] T109 [P] [US4] Treść: `corpus/zrodla/zatrucia/sprzecznosc-z-oryginalem.yaml` (zmienione postanowienia i stawki względem dokumentu oryginalnego)
- [ ] T110 [US4] Ustaw `Poison` w `corpus/przebieg.json` (5 rodzajów × 2 na typ), `generate`, przejrzyj i zacommituj `corpus/zatrute/**`; rozszerz `CorpusFullTests` o SC-023 (100% `places[].text` dosłownie w Markdown, w jednostce z manifestu) i SC-028 (≥ 2 na parę, `imitates` istnieje); próbka (T072) obejmuje pierwszy dokument każdej pary typ × rodzaj; nowe problemy biblioteki z zatrutymi układami → pary red/green jak T089

**Checkpoint**: komplet dokumentów zatrutych z prawdą referencyjną.

---

## Phase 7: User Story 5 — Rozbudowa korpusu bez pisania kodu i instrukcja (Priority: P3)

**Goal**: pełne parametry CLI, przebiegi większe niż treść (powtórzenia z raportem), instrukcja
`corpus/README.md`.

**Independent Test**: quickstart.md §5 (15 procedur po 40–50 stron, nowy blok bez kodu).

### Tests for User Story 5 ⚠️

- [ ] T111 [P] [US5] Testy w `ctests/Cli/ProgramOptionsTests.cs` (red): wszystkie opcje contracts/cli.md (`--types`, `--count`, `--pages`, `--reference-date`, `--versioned`, `--outdated`, `--contradictions`, `--poison <rodzaj>=<n>`/`none`, `--no-strict-uniqueness`, `--save-params`) nadpisują `--params`; błędne wartości → kod 2
- [ ] T112 [P] [US5] Testy w `ctests/Unit/Planning/NonStrictUniquenessTests.cs` (red, `zrodla-mini`): przebieg wymagający więcej treści niż warianty — z `StrictUniqueness` → kod 3 z liczbą brakujących bloków; bez — powtórzenia między dokumentami (nigdy w tym samym), `repeatedWordShare` per dokument i dla przebiegu w manifeście; zapisany przebieg korpusu nadal ściśle unikalny
- [ ] T113 [P] [US5] Test w `ctests/Unit/CorpusGeneratorExtensibilityTests.cs` (red): nowy **typ dokumentu** (wpis w `typy.yaml` + szablon w istniejącym stylu układu), szablon, blok i rodzaj zatrucia dodane wyłącznie jako pliki YAML w kopii `zrodla-mini` (katalog tymczasowy) pojawiają się w wyniku (`<nowy-typ>/`, `zatrute/<typ>/<nowy-rodzaj>/`) bez zmian kodu (FR-102, FR-134)

### Implementation for User Story 5

- [ ] T114 [US5] Zaimplementuj opcje w `src/LegalAgent.Corpus.Cli/Program.cs` — T111 green
- [ ] T115 [US5] Zaimplementuj tryb nieścisły i raport powtórzeń w `corpus-lib/Planning/CorpusPlanner.cs` / `corpus-lib/Validation/CorpusChecks.cs` / manifest (`repeatedWordShare`) — T112 green
- [ ] T116 [US5] Usuń zaszyte listy typów dokumentów i rodzajów zatruć (`DocumentType`, `PoisonKind` czytane z plików `typy.yaml` i `zatrucia/*.yaml`) — T113 green
- [ ] T117 [US5] Napisz `corpus/README.md`: cel i zawartość korpusu (układ katalogów, manifest — odsyłacz do schematu), wygenerowanie od nowa jednym poleceniem, `verify`, dodanie dokumentów (`--count`, nowy szablon), zmiana liczby stron (`--pages`), nowy typ dokumentu (szablon z istniejącymi stylami; kiedy potrzebny kod — nowy element układu), nowy szablon/blok/fakt (format, warianty, unikalność, bloki wspólne ≤ 20%), nowy rodzaj zatrucia, odświeżenie Markdown i manifestu (`refresh`), zaokrąglenia liczb z parametrów, pobranie aktów (curl z `--max-time`, sprawdzenie `%PDF-`); obecny korpus = przebieg z `przebieg.json`; walidacja SC-030: przejście instrukcji krok po kroku w czystym klonie (quickstart.md §5) i poprawki README

**Checkpoint**: generator wielokrotnego użytku z instrukcją.

---

## Phase 8: User Story 6 — Akty prawne pod tematykę bankową (Priority: P3)

**Goal**: `corpus/akty/` z 10 aktami (6 z testów + 4 z research.md R12), PDF + Markdown + `ZRODLA.md` +
wpisy manifestu ze źródłem.

**Independent Test**: każdy akt ma PDF, MD i wpis manifestu z `source.url` i `journal`.

### Tests for User Story 6 ⚠️

- [ ] T118 [P] [US6] Testy w `ctests/Unit/ActsTests.cs` (red, katalog tymczasowy z małym PDF aktu): `refresh` tworzy `<id>.md` i wpis manifestu `type: akty` z `source` (`journal`, `consolidatedTextDate`, `url`, `downloadedOn`, `notes`); `ZRODLA.md` odtwarzany z `akty.yaml` (tabela: plik, akt, publikator, źródło, pobrano, uwagi); brak PDF wymienionego w `akty.yaml` → kod 6 z nazwą pliku; generator nie wykonuje żadnego połączenia sieciowego

### Implementation for User Story 6

- [ ] T119 [US6] Zaimplementuj obsługę aktów w `corpus-lib/CorpusGenerator.cs` (`refresh`), `ManifestWriter` (`ActInfo`) i generowanie `corpus/akty/ZRODLA.md` — T118 green
- [ ] T120 [US6] Skopiuj 6 aktów z `ptests/Corpus/acts/*.pdf` do `corpus/akty/` (te same nazwy) i pobierz 4 akty (research.md R12: `dz-u-2025-644-aml`, `dz-u-2019-1781-ochrona-danych`, `dz-u-2026-823-reklamacje`, `dz-u-2025-720-kredyt-hipoteczny`) poleceniem z README (`curl --fail --max-time 60`, sprawdzenie `%PDF-`); uzupełnij `corpus/zrodla/akty.yaml` (10 wpisów; dla 6 aktów źródła z `ptests/Corpus/acts/SOURCES.md`; uwagi o nieuwzględnionych nowelizacjach: AML — Dz. U. 2025 poz. 1669, ochrona danych — Dz. U. 2026 poz. 252 i 548)
- [ ] T121 [US6] `refresh`; przejrzyj Markdown 4 nowych aktów (konwersja kompletna, brak ostrzeżeń krytycznych w raporcie; nowe defekty parsera → pary red/green jak T089, zgłoszone właścicielowi); zacommituj `corpus/akty/**` i manifest; `CorpusFullTests` obejmuje akty w SC-020 (komplet plików i wpisów)

**Checkpoint**: korpus kompletny.

---

## Phase 9: Polish & Cross-Cutting Concerns

- [ ] T122 [P] Dodaj do `.github/workflows/ci.yml` krok „Corpus full” po testach: `dotnet test LegalAgent.slnx -c Release --no-build --filter "Category=CorpusFull"` z `env: LEGALAGENT_CORPUS_FULL: 1`; (krok `verify` jest już z T072a)
- [ ] T123 [P] Rozszerz `CorpusFullTests` o SC-027 (0 nazw zabronionych w PDF i MD dokumentów syntetycznych) i SC-031 (bloki wspólne ≤ 20% słów, brak powtórzeń bloków niewspólnych między dokumentami bazowymi); test czasu SC-021 jako `[Trait("Category","Performance")]` (pełne `generate` do katalogu tymczasowego < 10 min)
- [ ] T124 [P] Zaktualizuj `README.md` w katalogu głównym: sekcja „Korpus syntetyczny” (cel, `corpus/README.md`, polecenia `generate`/`refresh`/`verify`, testy `CorpusFull`), nowe projekty w opisie solucji, licencja czcionek Noto (OFL) w nowej lokalizacji
- [ ] T125 Uruchom testy jak CI (`--filter "Category!=Performance"`, `Category=Performance`, `CorpusFull`, `verify`) na Windows; wypchnij gałąź i potwierdź zielone CI na Ubuntu (SC-021: identyczne pliki na obu systemach — `verify` w CI)
- [ ] T126 Dopisz sekcję „Stan prac i przekazanie” na końcu `specs/003-synthetic-bank-corpus/plan.md` (co zrobione, wyniki metryk, otwarte decyzje, poprawki biblioteki z R11/T089) i odhacz zadania

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Ph1)** → **Foundational (Ph2)** → historyjki.
- **US1 (Ph3)** po Ph2 — MVP; US2–US6 wymagają korpusu bazowego (T073).
- **US2 (Ph4)** po US1; jej poprawki biblioteki mogą trwać równolegle z US3/US4 (inne pliki), ale
  `refresh`/commit korpusu sekwencyjnie.
- **US3 (Ph5)** i **US4 (Ph6)** po US1; US4 korzysta z planowania US3 tylko pośrednio (zatrute mogą
  podrabiać dowolny dokument) — można je robić równolegle poza wspólnymi plikami
  (`CorpusPlanner.cs`, `ManifestWriter.cs` — kolejno).
- **US5 (Ph7)** po US4 (README opisuje zatrucia i wersje; T113 wymaga rodzajów zatruć z plików).
- **US6 (Ph8)** po US1 (manifest, `refresh` z T090) — niezależna od US3–US5.
- **Polish (Ph9)** po wszystkich.

### Graf

```text
Ph1 → Ph2 → US1 ─┬→ US2 ─────────────┐
                 ├→ US3 ─┐           │
                 ├→ US4 ─┴→ US5 ─────┼→ Polish
                 └→ US6 (po T090) ───┘
```

### Within Each User Story

- Test czerwony (commit `(red)`) → implementacja (commit green) → pełny zestaw testów.
- Pliki współdzielone (`CorpusPlanner.cs`, `DocumentComposer.cs`, `ManifestWriter.cs`,
  `CorpusGenerator.cs`, `Program.cs`, `corpus/przebieg.json`, `corpus/manifest.json`) zmieniane
  sekwencyjnie.
- Po każdej historyjce zmieniającej korpus: `generate`/`refresh`, przegląd diffu `corpus/`, `verify`,
  próbka zielona, commit danych osobno od kodu (`data: …`).

### Parallel Opportunities

- Ph1: T002, T003 równolegle po T001.
- Ph2: T005, T007, T009, T011, T013, T015, T017, T019, T021, T023, T025 (testy) równolegle; implementacje
  po swoich testach; T016 przed T018/T020/T022 (wspólny `Typesetter`).
- US1: testy T027–T033 równolegle; **treść T042–T071 (30 zadań) w pełni równolegle** po T041 i T040
  (każde we własnym pliku szablonu i bloków) — najlepszy kandydat do delegowania subagentom.
- US2: pary T077/T078 … T087/T088 niezależne od siebie (różne etapy parsera — przy wspólnym pliku
  kolejno).
- US4: treść T105–T109 równolegle.
- US5: T111–T113 równolegle.
- Polish: T122–T124 równolegle.

---

## Parallel Example: User Story 1

```text
# Red — testy jednocześnie:
T027 RunParameters   T028 CorpusPlanner   T029 DocumentComposer
T030 PageFitter      T031 ManifestWriter  T032 CorpusGenerator   T033 CLI

# Green — kolejno wg zależności:
T034 → T035 → T036 → T037 → T038 → T039 → T040

# Treść — po T041, wszystkie naraz (np. 3 subagenty × 10 szablonów):
T042…T051 regulaminy   T052…T061 taryfy   T062…T071 procedury
```

## Parallel Example: User Story 4

```text
T099 PoisonPlanner   T100 PoisonOperations   T101 ManifestPoison      (red)
→ T102 → T103 → T104                                                    (green)
T105 falszywe-stawki  T106 polecenia-dla-ai  T107 podszywanie  T108 nieaktualny  T109 sprzecznosc
→ T110 generate + commit
```

---

## Implementation Strategy

### MVP First (US1)

1. Ph1 + Ph2 (konstruktor PDF przeniesiony bez zmian goldenów; składanie dokumentów w 6 stylach).
2. US1 → **STOP i walidacja**: `generate` tworzy 10 × 3 dokumentów 20–30 stron, `verify` = 0,
   drugi przebieg bez zmian. Wartość dla aplikacji RAG już na tym etapie.

### Incremental Delivery

1. MVP (US1) → korpus bazowy dla RAG.
2. + US2 → potwierdzona jakość Markdown (metryki), poprawki biblioteki.
3. + US3 → wersje, nieaktualne, sprzeczne.
4. + US4 → dokumenty zatrute z prawdą referencyjną.
5. + US5 → generator wielokrotnego użytku z README.
6. + US6 → akty prawne; + Polish → CI, README, przekazanie.

### Notes

- [P] = różne pliki, brak zależności od niezakończonych zadań.
- Implementacja wyłącznie przez `/speckit-implement` z zakresem zadań; po każdej fazie odhaczanie `[X]`.
- Zmiana zachowania sprzecznego ze spec → najpierw doprecyzowanie FR w spec.md (w tym samym commicie co test red).
- Markdown korpusu zawiera wyłącznie tekst PDF — biblioteka niczego nie dopisuje (FR-162; zasada z 002 FR-091).
- Treść (T041–T071, T097, T105–T109) to największy nakład pracy: pisać partiami, sprawdzać po każdym
  szablonie `generate --types <typ>` (zakres stron, unikalność, nazwy zabronione).
