# Data Model: syntetyczny korpus banku (spec 003)

**Feature**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md) | **Research**: [research.md](./research.md)

Model dotyczy nowej biblioteki `LegalAgent.Corpus`. Model publiczny biblioteki `LegalAgent.PdfParser`
się nie zmienia (ewentualne poprawki R11 są wewnętrzne). Formaty plików:
[content-format](./contracts/content-format.md), [manifest](./contracts/manifest.md),
[CLI](./contracts/cli.md), [układ katalogów](./contracts/corpus-layout.md).

## 1. Treść źródłowa (wczytywana z `corpus/zrodla/`)

### ContentLibrary
Całość wczytanych plików źródłowych; walidowana przy wczytaniu (błąd = komunikat z plikiem i ścieżką
w YAML, kod wyjścia 2).

| Pole | Typ | Reguły |
|------|-----|--------|
| `Facts` | `FactCatalog` | z `fakty.yaml` |
| `Templates` | `IReadOnlyList<DocumentTemplate>` | z `szablony/*.yaml`; identyfikatory unikalne |
| `Blocks` | `IReadOnlyList<ContentBlock>` | z `bloki/**/*.yaml`; identyfikatory unikalne |
| `PoisonPatterns` | `IReadOnlyList<PoisonPattern>` | z `zatrucia/*.yaml` |
| `ForbiddenNames` | `IReadOnlyList<string>` | z `zabronione.yaml`; porównanie bez wielkości liter i znaków diakrytycznych |
| `Acts` | `IReadOnlyList<ActSource>` | z `akty.yaml` |
| `DocumentTypes` | `IReadOnlyList<DocumentTypeDef>` | z `typy.yaml`; `Id` i `Prefix` unikalne |

### Fact
| Pole | Typ | Reguły |
|------|-----|--------|
| `Id` | string | kropkowany, np. `oplata.karta.wydanie-duplikatu`; unikalny |
| `Kind` | enum `Kwota`, `Procent`, `Termin`, `Tekst`, `Data` | decyduje o formacie wyjściowym („15,00 zł”, „1,5%”, „14 dni”) |
| `Values` | lista `(Od: data?, Wartość)` | uporządkowana po dacie; pierwsza bez daty = wartość bazowa |
| `Alternatives` | lista wartości | pula wartości dla sprzeczności i zatruć (różnych od `Values`) |

Wartość faktu dla dokumentu = wartość obowiązująca w dniu początku obowiązywania dokumentu, chyba że
dokument ją nadpisuje (`FactOverride`).

### DocumentTypeDef (z `zrodla/typy.yaml`)
Typ dokumentu jest definiowany w plikach, nie w kodzie (FR-102): nowy typ korzystający z istniejących
stylów układu nie wymaga zmiany kodu.

| Pole | Typ | Reguły |
|------|-----|--------|
| `Id` | string | nazwa katalogu, np. `regulaminy`; tylko `[a-z-]`; unikalny |
| `Prefix` | string | prefiks id i plików, np. `REG`; 2–4 wielkie litery ASCII; unikalny |
| `DesignationPattern` | string | np. `BP/{prefiks}/{nn}` |
| `Name` | string | nazwa w README i komunikatach, np. „regulamin” |
| `RequiredElements` | lista | elementy obowiązkowe szablonu typu (np. `metryczka`, `schemat`, `lista-kontrolna`, `zalacznik`, `przypis`, `definicje`, `tabela-wielostronicowa`, `segmenty`); sprawdzane przez `CorpusChecks.TemplateStructure` (FR-110 – FR-112) |

### DocumentTemplate
| Pole | Typ | Reguły |
|------|-----|--------|
| `Id` | string | np. `regulamin-karty` |
| `Type` | id z `typy.yaml` (np. `regulaminy`, `taryfy`, `procedury`) | (akt prawny nie ma szablonu) |
| `Topic` | string | temat, np. `karty`, `aml`; dzieli pule bloków |
| `TitleVariants` | lista tekstów z parametrami | tytuł dokumentu |
| `Layouts` | lista `LayoutStyleId` | dozwolone style układu; wybór ziarnem |
| `Sections` | lista `SectionSlot` | kolejność; slot: `Kind` (rozdział, sekcja procedury, sekcja taryfy, załącznik), `Required` blok(i), `Optional` (kategorie bloków do dobierania), `Min`/`Max` liczby bloków |
| `Front` | `FrontMatterSpec` | okładka i/lub metryczka: które pola (oznaczenie, wersja, daty, właściciel, zatwierdzający, historia zmian) |

### ContentBlock
| Pole | Typ | Reguły |
|------|-----|--------|
| `Id` | string | unikalny |
| `Types`, `Topics` | zbiory (typy = id z `typy.yaml`) | gdzie blok może wystąpić |
| `Category` | string | np. `paragraf`, `definicja`, `pozycja-taryfy`, `krok`, `lista-kontrolna`, `zalacznik` |
| `Shared` | bool | `true` = blok wspólny (FR-103a): może wystąpić w wielu dokumentach |
| `Elements` | lista `ContentElement` | treść z wariantami `{a|b}` i parametrami `{{…}}` |
| `Unit` | string? | wzorzec oznaczenia jednostki („§ {n}.”, „{n}.”), numerowany przy składaniu |

### ContentElement (drzewo treści)
`Heading(level, label?, text)`, `Paragraph(inlines)`, `ListItem(label, level, inlines, children)`,
`Footnote(marker, inlines)`, `Table(columns, headerRow, rows, grid: bool, footnotes)`,
`StepScheme(steps: (name, explanation[]))`, `Checklist(items, form: wektor|tekst|tabela)`,
`KeyValueTable(pairs)` (metryczka), `Callout(inlines)` (ramka). `Inline` = tekst + styl (pogrubienie,
kursywa) + odniesienie do przypisu.

### PoisonPattern
| Pole | Typ | Reguły |
|------|-----|--------|
| `Id` | string | unikalny |
| `Kind` | `PoisonKind` = `falszywe-stawki`, `polecenia-dla-ai`, `podszywanie`, `nieaktualny-jako-obowiazujacy`, `sprzecznosc-z-oryginalem` (lista otwarta: nowe rodzaje z plików, FR-134) | nazwa katalogu |
| `Types` | zbiór id z `typy.yaml` | dla jakich typów |
| `Placements` | zbiór `PoisonPlacement` = `Akapit`, `Przypis`, `KomorkaTabeli`, `Metryczka`, `Okladka`, `Ramka` | gdzie można wstawić |
| `Goal` | `AiGoal`? = `zmiana-odpowiedzi`, `ignorowanie-zrodel`, `ukrycie-zrodla`, `dzialanie-poza-zakresem`, `podszycie-pod-polecenie` | wymagany dla `polecenia-dla-ai` (FR-132a) |
| `Operation` | `Insert` (tekst), `OverrideFact`, `ReplaceFrontMatter`, `ShiftValidity` | sposób zatrucia |
| `TextVariants` | lista tekstów | dla `Insert`/`ReplaceFrontMatter`; dane fikcyjne (FR-105) |
| `Description` | tekst | wzór opisu do manifestu |

### ActSource
`Id` (np. `AKT-AML`), `Title`, `Journal` (np. „Dz. U. 2025 poz. 644”), `ConsolidatedDate`, `Url`,
`DownloadedOn`, `File` (nazwa w `corpus/akty/`), `Notes` (np. nowelizacje nieuwzględnione).

## 2. Przebieg i plan

### RunParameters (`corpus/przebieg.json`, nadpisywane opcjami CLI)
| Pole | Typ | Domyślnie (zapisany przebieg) | Walidacja |
|------|-----|-------------------------------|-----------|
| `Seed` | ulong | `20261008` | — |
| `ReferenceDate` | data | `2026-10-01` | — |
| `Types` | zbiór id z `typy.yaml` | wszystkie 3 | niepusty; każdy istnieje w `typy.yaml` |
| `DocumentsPerType` | int | 10 | 1–500 |
| `Pages` | `(Min, Max)` | 20–30 | 1 ≤ Min ≤ Max ≤ 500 |
| `VersionedShare` | procent | 30% (→ 3 z 10) | 0–100; liczba = zaokrąglenie w górę |
| `MaxVersions` | int | 3 | 2–5 |
| `OutdatedPerType` | int | 2 | ≤ DocumentsPerType − wersjonowane |
| `ContradictionPairsPerType`, `CrossTypeContradictionPairs` | int | 1, 1 | — |
| `Poison` | lista `(Kind, PerType)` | 5 rodzajów × 2 | rodzaj musi mieć wzorce dla typu |
| `StrictUniqueness` | bool | `true` | `true` = brak powtórzeń bloków niewspólnych (FR-103b) |
| `MaxSharedShare` | procent | 20% | FR-103a |
| `OutputDirectory` | ścieżka | `corpus` | — |
| `ContentDirectory` | ścieżka | `corpus/zrodla` | — |
| `ParserOptions` | obiekt | domyślne `PdfParserOptions` (znaczniki stron włączone) | tylko pola `PdfParserOptions`; zapisywane w manifeście `run.parameters` (FR-107) |

Zaokrąglenia i rozdział liczb opisane w README; wszystkie wynikające liczby są deterministyczne.

### CorpusPlan → DocumentPlan
Wynik `CorpusPlanner` (bez składu stron).

| Pole `DocumentPlan` | Typ | Reguły |
|---------------------|-----|--------|
| `Id` | string | `REG-03`, `TAR-07`, `PRO-10`; wersja: `REG-03-w2`; zatruty: `ZAT-REG-POL-01` |
| `Designation` | string | oznaczenie w treści, np. `BP/REG/03`; wspólne dla wersji; zatruty — jak dokument podrabiany (lub zmienione przez wzorzec) |
| `Type`, `Template`, `LayoutStyle` | — | z ziarna dokumentu |
| `Version` | int | 1… |
| `ValidFrom`, `ValidTo?` | daty | wersje: ciągłe i rozłączne (`ValidTo(n) + 1 dzień = ValidFrom(n+1)`) |
| `Status` | `Obowiazujacy` / `Nieaktualny` | względem `ReferenceDate` |
| `PreviousVersionId` | string? | — |
| `BlockPool` | lista id bloków | przydział z góry (R3); wspólne + niewspólne |
| `FactOverrides` | lista `FactOverride(FactId, Value, Reason: Wersja/Sprzecznosc/Zatrucie)` | — |
| `ContradictsWith` | lista `(DocId, FactId lub BlockId)` | para sprzeczna |
| `Poison` | `PoisonPlan?` | rodzaj, wzorzec, wariant, miejsce docelowe, `ImitatesId` |
| `TargetPages` | int | z zakresu, ziarnem dokumentu |
| `Seed` | ulong | pochodna (R3) |

Przejścia stanu dokumentu: wersja 1 → … → wersja N (ostatnia obowiązująca, chyba że cały dokument
nieaktualny). Wersje wcześniejsze mają `Status = Nieaktualny`. Dokument nieaktualny (FR-121) ma
`ValidTo < ReferenceDate` i nie ma następcy w korpusie.

## 3. Wynik składu

### ComposedDocument
Drzewo `ContentElement` z nadanymi numerami jednostek (§ N., N.N.), wartościami faktów i wybranymi
wariantami; każdy element ma `ElementId` (stabilny w obrębie dokumentu) i znacznik `Poison` dla elementów
wstawionych/zmienionych przez zatrucie.

### TypesetResult
| Pole | Typ | Opis |
|------|-----|------|
| `Pdf` | byte[] | po normalizacji `/ID` (R4) |
| `PageCount` | int | sprawdzane względem zakresu |
| `Truth` | `DocumentTruth` | prawda referencyjna |
| `ElementPages` | `ElementId → (pierwsza, ostatnia strona)` | do miejsc zatruć i zmian w manifeście |
| `BlockWordCounts` | `BlockId → słowa` | do SC-031 (udział bloków wspólnych) |

### DocumentTruth (prawda referencyjna, FR-106)
| Pole | Opis |
|------|------|
| `Words` | słowa treści w kolejności czytania (bez artefaktów stron) |
| `Headings` | `(Level, Label, Text)` — rozdziały, § N., sekcje procedur/taryf, załączniki; poziom względem tytułu `#` |
| `ListItems` | `(Label, Depth, FirstWords)` |
| `Tables` | `(Header[], Rows[][])` po scaleniu stron, bez powtórzonych nagłówków |
| `Artifacts` | teksty nagłówków/stopek/numerów stron |
| `PoisonTexts` | `(ElementId, dosłowny tekst)` |

## 4. Manifest (`corpus/manifest.json`)
Szczegóły pól — [contracts/manifest.md](./contracts/manifest.md). Encje: `Manifest { Run, Documents[] }`,
`ManifestDocument` (wspólne pola wszystkich dokumentów), `VersionChange`, `Contradiction`, `PoisonInfo`
z `PoisonPlace[]`, `ActInfo`. Kolejność wpisów: akty, regulaminy, taryfy, procedury, zatrute — w
obrębie grupy porządek porządkowy (ordinal) identyfikatorów.

## Reguły walidacji (przekrój)

- Każdy blok z `Unit` ma unikalne oznaczenie w dokumencie po numeracji.
- Odwołania w treści (`{{ref:…}}` do § / pozycji / załącznika / dokumentu / aktu) muszą wskazywać
  istniejącą jednostkę po składzie; inaczej błąd przebiegu (FR-114).
- Tekst wyjściowy (PDF i Markdown dokumentów syntetycznych) nie zawiera nazw z `ForbiddenNames` (FR-105).
- `StrictUniqueness`: wyrenderowany tekst bloku niewspólnego (znormalizowany: małe litery, pojedyncze
  spacje) nie powtarza się w dwóch dokumentach bazowych; inaczej błąd (FR-103b); bez `StrictUniqueness`
  — raport udziału powtórzeń.
- Udział słów bloków wspólnych ≤ `MaxSharedShare` w każdym dokumencie bazowym (FR-103a).
- Dokument zatruty: `ImitatesId` istnieje; liczba stron w zakresie typu; każde `PoisonPlace` ma stronę.
