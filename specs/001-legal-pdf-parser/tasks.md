---

description: "Task list for LegalAgent.PdfParser"
---

# Tasks: LegalAgent.PdfParser — konwersja PDF aktów prawnych i regulaminów do Markdown

**Input**: Design documents from `specs/001-legal-pdf-parser/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/), [quickstart.md](./quickstart.md)

**Tests**: OBOWIĄZKOWE — konstytucja, zasada I (TDD, NON-NEGOTIABLE). W każdej fazie zadania testowe
poprzedzają implementację; test MUSI najpierw zakończyć się niepowodzeniem (Red), potem minimalna
implementacja (Green), potem refaktoryzacja. Testy offline i deterministyczne.

**Organization**: Zadania pogrupowane wg historyjek użytkownika (US1–US6 ze spec.md).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: można wykonać równolegle (inne pliki, brak zależności od niezakończonych zadań)
- **[Story]**: historyjka (US1…US6)

## Path Conventions

- Biblioteka: `src/LegalAgent.PdfParser/`
- CLI: `src/LegalAgent.PdfParser.Cli/`
- Testy: `tests/LegalAgent.PdfParser.Tests/`
- Syntetyczne PDF w testach budowane wyłącznie przez `tests/LegalAgent.PdfParser.Tests/Fixtures/SyntheticPdfBuilder.cs` (T009).
- Konwencje kodu: rekordy niezmienne dla modelu publicznego, `CultureInfo.InvariantCulture` / `StringComparison.Ordinal*` wszędzie, LF w wyniku, brak `Task.Run` w bibliotece (research R4, R13).

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Solucja, projekty, przypięte zależności, CI.

- [X] T001 Create `global.json` at repo root pinning SDK `10.0.301` with `"rollForward": "latestFeature"` (research R1)
- [X] T002 Create `Directory.Build.props` at repo root: `TargetFramework=net9.0`, `LangVersion=13`, `Nullable=enable`, `ImplicitUsings=enable`, `TreatWarningsAsErrors=true`, `Deterministic=true`, `ManagePackageVersionsCentrally=true`, `AnalysisLevel=latest-recommended`, and `<WarningsAsErrors>` for CA1304;CA1305;CA1307;CA1309;CA1310 (plan.md)
- [X] T003 Create `Directory.Packages.props` at repo root with pinned versions: `PdfPig` 0.1.16; `Microsoft.Extensions.DependencyInjection.Abstractions`, `Microsoft.Extensions.Options`, `Microsoft.Extensions.DependencyInjection`, `Microsoft.Extensions.Configuration.EnvironmentVariables`, `Microsoft.Extensions.Configuration.Binder` 9.0.20; `xunit.v3`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk` at latest stable versions on nuget.org at implementation time (exact versions, no ranges)
- [X] T004 Create projects `src/LegalAgent.PdfParser/LegalAgent.PdfParser.csproj` (classlib; refs PdfPig, DI.Abstractions, Options; `GenerateDocumentationFile=true`; `InternalsVisibleTo` LegalAgent.PdfParser.Tests), `src/LegalAgent.PdfParser.Cli/LegalAgent.PdfParser.Cli.csproj` (console, `AssemblyName=legalagent-pdf`, refs library + DI + Configuration.EnvironmentVariables + Configuration.Binder), `tests/LegalAgent.PdfParser.Tests/LegalAgent.PdfParser.Tests.csproj` (xunit.v3, refs library and CLI); create `LegalAgent.slnx` containing all three; verify `dotnet build LegalAgent.slnx` succeeds
- [X] T005 [P] Append to `.gitignore`: `*.actual.md`, `tests/LegalAgent.PdfParser.Tests/Corpus/private/`
- [X] T006 [P] Create `.github/workflows/ci.yml`: ubuntu-latest, `actions/setup-dotnet` with `9.0.x` and SDK from `global.json`, steps `dotnet build LegalAgent.slnx -c Release` and `dotnet test LegalAgent.slnx -c Release --filter "Category!=Performance"`, plus separate step running `Category=Performance` (research R16)
- [X] T007 [P] Add fonts `NotoSans-Regular.ttf`, `NotoSans-Bold.ttf`, `NotoSans-Italic.ttf` and `OFL.txt` to `tests/LegalAgent.PdfParser.Tests/Fixtures/Fonts/`, copied to output directory via the test csproj (research R14)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Narzędzia testowe, model publiczny, opcje, wyjątki, potok, ucieczka Markdown.

**⚠️ CRITICAL**: Żadna historyjka nie startuje przed zakończeniem tej fazy.

- [X] T008 [P] Write tests for the synthetic PDF builder in `tests/LegalAgent.PdfParser.Tests/Fixtures/SyntheticPdfBuilderTests.cs`: generated PDF opens with PdfPig, Polish text „zażółć gęślą jaźń” round-trips, letter `PointSize` and bold font name match what was requested, line segments appear as page paths, rotated text has non-horizontal `TextOrientation`
- [X] T009 Implement `tests/LegalAgent.PdfParser.Tests/Fixtures/SyntheticPdfBuilder.cs` on top of PdfPig `PdfDocumentBuilder` with Noto Sans TrueType fonts: fluent API `Page(width = 595, height = 842)`, `Text(x, yFromTop, text, size = 11, bold = false, italic = false)`, `RotatedText(...)`, `InvisibleText(...)`, `HLine/VLine(...)`, `Image(...)` (embedded PNG fixture), `BlankPage()`, `Build(): byte[]`, plus helpers `RunningHeader(text with {n})`, `PageNumberFooter(format)` applied to all pages
- [X] T010 [P] Implement golden-file helper `tests/LegalAgent.PdfParser.Tests/Fixtures/GoldenFile.cs`: `AssertMatches(string actual, string expectedPath)` normalizing nothing (byte-exact, LF), on mismatch writes `<name>.actual.md` beside and fails with first differing line; when env `UPDATE_GOLDEN=1` overwrites expected file instead
- [X] T011 [P] Create public model records in `src/LegalAgent.PdfParser/Model/` exactly per data-model.md §1: `PdfConversionResult.cs`, `LegalDocument.cs`, `SourceInfo.cs`, `Section.cs` + `SectionKind.cs` (`DocumentTitle, Book, Part, Division, Chapter, Subchapter, Article, Paragraph, Typographic`), `ContentBlock.cs` (`ParagraphBlock`, `ListBlock`, `TableBlock`, `SkippedPageBlock` + `SkipReason { NoTextLayer, PageReadError }`), `ListItem.cs` + `ListLabelKind.cs` (`Bullet, Dash, ArabicParen, ArabicDot, LetterParen, Roman, Outline`), `TableRow.cs`, `TableCell.cs` (`ColumnSpan`), `Inline.cs` (`TextRun` + `[Flags] TextStyle { None, Bold, Italic }`, `FootnoteRef`, `PageBreak`), `Footnote.cs` (`IsOrphan`), `PageRange.cs` (record struct; invariant `1 ≤ First ≤ Last`), `ConversionReport.cs` (+ `SkippedPage`, `ArtifactSummary` with `ArtifactKind { RunningHeader, RunningFooter, PageNumber }`, `ConversionWarning { Code, PageNumber?, Message }`); all with XML-doc
- [X] T012 [P] Create exceptions in `src/LegalAgent.PdfParser/Exceptions/` per contracts/public-api.md: `PdfParserException`, `InvalidPdfException` (+ `InvalidPdfReason { Empty, NotPdf, Corrupted }`), `PdfEncryptedException`, `PdfPageReadException` (`PageNumber`), `PdfLimitExceededException` (`Limit: PdfLimit { InputSize, PageCount, Duration }`, `Configured`, `Measured`), `PdfNoTextException`; Polish messages
- [X] T013 [P] Create options in `src/LegalAgent.PdfParser/Options/`: `PdfParserOptions.cs` with groups `LimitsOptions`, `NormalizationOptions`, `ArtifactOptions`, `LayoutOptions`, `HeadingOptions`, `ListOptions`, `TableOptions`, `RenderingOptions` (+ `FootnotesPlacement { EndOfSection }`), `FootnoteOptions` and root `AllowPartialResult`; defaults exactly as data-model.md §2 table (e.g. `MaxInputBytes = 104_857_600`, `MaxPages = 2000`, `MaxDuration = 120 s`, `MarginZoneRatio = 0.08`, `Similarity = 0.85`, `SizeRatio = 1.15`, `ColumnTolerance = 0.03`, `PageMarkers = true`), including `LayoutOptions.ShortLineRatio = 0.75`, `GutterMinWidthRatio = 0.02`, `GutterMinHeightRatio = 0.6`, `ColumnMinLineWidthRatio = 0.3`, `HeadingOptions.GapFactor = 1.3`, `CenterTolerance = 0.05`, `TableOptions.RowMergeGapFactor = 1.2`; add deep `Clone()`
- [X] T014 Write tests `tests/LegalAgent.PdfParser.Tests/Unit/Options/PdfParserOptionsValidatorTests.cs`: defaults valid; ratio values outside (0, 1] invalid; non-positive counts/sizes invalid; `MaxTypographicDepth` outside 1–6 invalid; null limits allowed (disabled); `Clone()` is deep
- [X] T015 Implement `src/LegalAgent.PdfParser/Options/PdfParserOptionsValidator.cs` (`IValidateOptions<PdfParserOptions>`) to pass T014
- [X] T016 [P] Create internal layout model in `src/LegalAgent.PdfParser/Layout/` per data-model.md §3: `LayoutPage.cs`, `LayoutGlyph.cs`, `LayoutWord.cs`, `LayoutLine.cs` (+ `LineZone { Header, Body, Footer }`, `LineRole { Unknown, Artifact, Footnote, Table, ListItem, ListContinuation, Heading, Body }`), `LineSegment.cs`, `LayoutBlock.cs`, `Rect.cs`, `Segment.cs` (Y-down coordinates, points)
- [X] T017 [P] Create pipeline contracts in `src/LegalAgent.PdfParser/Pipeline/`: `IPipelineStage.cs` (`int Order`, `void Execute(PipelineContext)`), `PipelineContext.cs` (Options read-only, Source, Pages, Report, CancellationToken, BodyStyle, Document), `ReportBuilder.cs` (warnings, artifact counts, deterministic ordering on `Build()`), `StageOrder.cs` constants 100…1100 per data-model.md §4
- [X] T018 Write tests `tests/LegalAgent.PdfParser.Tests/Unit/Pipeline/PipelineRunnerTests.cs`: stages run ascending by `Order`, ties broken by full type name ordinal; cancellation token checked before each stage; exception from stage that is not `PdfParserException` is wrapped preserving inner; `OperationCanceledException` propagates unchanged
- [X] T019 Implement `src/LegalAgent.PdfParser/Pipeline/PipelineRunner.cs` to pass T018
- [X] T020 [P] Write tests `tests/LegalAgent.PdfParser.Tests/Rendering/MarkdownEscaperTests.cs` per contracts/markdown-output.md „Ucieczka znaków”: escapes `\ * _ [ ] < > `` ` ``; line-start `#`, `+`, `-`, `>`, `=`, `\d+[.)]` (`2024. r.` → `2024\. r.`); table mode escapes `|`; list labels `1)` → `1\)`, `2.` → `2\.`, `a)` → `a\)`
- [X] T021 Implement `src/LegalAgent.PdfParser/Rendering/MarkdownEscaper.cs` to pass T020

**Checkpoint**: Fundament gotowy — historyjki mogą startować.

---

## Phase 3: User Story 1 - Czysty Markdown z PDF bez artefaktów stron (Priority: P1) 🎯 MVP

**Goal**: Strumień PDF → ciągły Markdown akapitów bez nagłówków/stopek/numerów stron, ze sklejonymi akapitami między stronami i dzieleniem wyrazów, ze znacznikami `<!-- page: N -->`, deterministycznie.

**Independent Test**: Syntetyczny 6-stronicowy PDF z nagłówkiem „Dziennik Ustaw – {n} – Poz. 1234”, stopką „Strona {n} z 6”, akapitem przechodzącym przez stronę i wyrazem z dzieleniem → wynik zgodny z `tests/LegalAgent.PdfParser.Tests/Integration/Expected/us1-artifacts.expected.md`; dwa uruchomienia identyczne.

### Tests for User Story 1 ⚠️ (write first, must fail)

- [X] T022 [P] [US1] Write `tests/LegalAgent.PdfParser.Tests/Unit/Input/PdfInputReaderTests.cs`: empty stream → `InvalidPdfException(Empty)`; bytes without `%PDF-` header → `InvalidPdfException(NotPdf)`; reads from current position to end; non-seekable stream (wrapper with `CanSeek=false`) accepted; caller stream not disposed; `MaxInputBytes` exceeded → `PdfLimitExceededException(InputSize)` with Configured/Measured and reading stops at limit+1 byte; `MaxInputBytes = null` disables; SHA-256 lowercase hex computed; `!CanRead` → `ArgumentException`
- [X] T023 [P] [US1] Write `tests/LegalAgent.PdfParser.Tests/Unit/Input/PdfDocumentOpenerTests.cs`: truncated/garbage PDF body → `InvalidPdfException(Corrupted)`; encrypted fixture `Corpus/errors/encrypted.pdf` → `PdfEncryptedException`; page count above `MaxPages` → `PdfLimitExceededException(PageCount)` before any page is processed; `SourceInfo.PageCount`, `PdfTitle`, `ByteLength` populated
- [X] T024 [P] [US1] Add error fixtures in `tests/LegalAgent.PdfParser.Tests/Corpus/errors/`: `encrypted.pdf` (user password, produced once with `qpdf --encrypt` from a synthetic PDF), `not-a-pdf.txt`, `truncated.pdf`, plus `README.md` documenting how each was produced
- [X] T025 [P] [US1] Write `tests/LegalAgent.PdfParser.Tests/Unit/Stages/PageExtractionStageTests.cs`: glyphs carry `PointSize` (not `FontSize`), bold detected via font name tokens `Bold|Black|Heavy|Semibold|Demi` after subset prefix `ABCDEF+` removal and via `IsBold`, italic via `Italic|Oblique`; Y converted to top-down; rotated text dropped and counted in `DroppedTextCount` (FR-013); invisible render mode dropped; horizontal/vertical ruling segments extracted; cancellation checked before each page
- [X] T026 [P] [US1] Write `tests/LegalAgent.PdfParser.Tests/Unit/Stages/TextNormalizationStageTests.cs` (FR-010): ligatures ﬀ ﬁ ﬂ ﬃ ﬄ ﬅ ﬆ expanded; `a` + U+0328 → `ą` (NFC); NBSP, U+2009, U+202F → space; superscript digits `¹²³` preserved (no NFKC); raised small-font digits after `Art. 12`/`§ 5` converted to Unicode superscripts (research R11)
- [X] T027 [P] [US1] Write `tests/LegalAgent.PdfParser.Tests/Unit/Stages/LineAssemblyStageTests.cs`: words reconstructed from glyph gaps when no space glyphs (FR-011, no glued/spaced-out words); glyphs grouped into one line when vertical overlap ≥ 50% or baseline delta ≤ 30% of smaller font (FR-030); superscript footnote marker stays in its line; segments split on horizontal gap > `CellGapFactor` × mean space width; line zones Header/Footer = top/bottom 8% of each page height, computed per page (landscape page mixed with portrait)
- [X] T028 [P] [US1] Write `tests/LegalAgent.PdfParser.Tests/Unit/Text/FingerprintAndSimilarityTests.cs`: fingerprint lowercases invariantly, replaces `\d+` with `#`, collapses whitespace, trims edge punctuation („Dziennik Ustaw – 3 – Poz. 1234” ≡ „Dziennik Ustaw – 17 – Poz. 1234”); normalized Levenshtein similarity values for known pairs; ≥ 0.85 threshold behaviour
- [X] T029 [P] [US1] Write `tests/LegalAgent.PdfParser.Tests/Unit/Text/PageNumberPatternsTests.cs` (FR-023): matches `3`, `iv`, `- 3 -`, `– 3 –`, `3 / 40`, `Strona 3 z 40`, `Str. 3`, `s. 3/40`; does not match `3 zł`, `art. 3`, `2024`; offset detection = mode of (printed − physical) across document
- [X] T030 [P] [US1] Write `tests/LegalAgent.PdfParser.Tests/Unit/Stages/ArtifactRemovalStageTests.cs`: (a) header „Dziennik Ustaw – {n} – Poz. 1234” on all 6 pages removed, including page 1 variant where the header is embedded in a longer line (substring fingerprint match, remainder of the line kept as content — FR-022); (b) mirrored odd/even headers removed; (c) running header containing current chapter name with ≥ 85% similarity removed; (d) bank registry footer repeated in footer zone removed; (e) page-number footers in all FR-023 formats removed, consistent with offset; (f) margin-zone line occurring once (last line of paragraph, heading at page bottom) kept (FR-024); (g) body line „(uchylony)” repeated on many pages kept (not in margin zone); (h) 2-page document: only page numbers removed (FR-025); (i) same text at varying Y beyond 2% tolerance kept; (j) report `RemovedArtifacts` lists pattern, kind, occurrences, pages sorted by first page then pattern (FR-027); (k) `Artifacts.Enabled = false` removes nothing
- [X] T031 [P] [US1] Write `tests/LegalAgent.PdfParser.Tests/Unit/Stages/ReadingOrderStageTests.cs` (FR-031): two-column page (gutter ≥ 2% width through ≥ 60% height, lines ≥ 30% width) read left column then right; single-column page untouched; short multi-segment lines (table-like) never split into columns; `DetectColumns = false` disables
- [X] T032 [P] [US1] Write `tests/LegalAgent.PdfParser.Tests/Unit/Stages/BlockAssemblyStageTests.cs`: lines merged into paragraph when gap ≤ 1.5× typical leading and same font/indent (FR-032); new paragraph on gap > 1.5× leading or after a line ending with period that is shorter than 75% of column width (`ShortLineRatio`); paragraph continues across page when last line does not end a sentence and next starts lowercase or with body indent (FR-033), inserting `PageBreak(N)` inline at word boundary (FR-002a); hyphenation `przedsiębior-`/`ca` → `przedsiębiorca`, kept for `e-mail`, `biało-czerwony` (exception list), capitalized prefix `Bielsko-`/`Biała` and abbreviations (FR-012); inline bold/italic runs preserved as `TextStyle`
- [X] T033 [P] [US1] Write `tests/LegalAgent.PdfParser.Tests/Rendering/MarkdownRendererParagraphTests.cs`: paragraphs separated by exactly one blank line; `**bold**`, `*italic*`, `***both***` with whitespace moved outside markers; page marker before first block of a page on own line, inline marker between words; `PageMarkers = false` emits none; output ends with single `\n`, LF only; invariants 1, 2, 5 from contracts/markdown-output.md
- [X] T034 [US1] Write integration test `tests/LegalAgent.PdfParser.Tests/Integration/ArtifactCleanupIntegrationTests.cs` building the Independent Test PDF via `SyntheticPdfBuilder` and asserting golden `tests/LegalAgent.PdfParser.Tests/Integration/Expected/us1-artifacts.expected.md` (hand-written first) via `GoldenFile`
- [X] T035 [US1] Write `tests/LegalAgent.PdfParser.Tests/DeterminismTests.cs`: converting the same bytes twice (and with a fresh converter instance) yields identical `Markdown`, identical `Document` (record equality via JSON serialization) and identical `Report` excluding `Elapsed` (FR-008, SC-006)

### Implementation for User Story 1

- [X] T036 [P] [US1] Implement `src/LegalAgent.PdfParser/Input/PdfInputReader.cs` (async `CopyToAsync` into bounded buffer, header check, SHA-256) to pass T022 (research R4)
- [X] T037 [US1] Implement `src/LegalAgent.PdfParser/Input/PdfDocumentOpener.cs` (`PdfDocument.Open(byte[], ParsingOptions)`, map PdfPig encryption/parse exceptions, page limit) to pass T023
- [X] T038 [US1] Implement `src/LegalAgent.PdfParser/Stages/PageExtractionStage.cs` (Order 100; text extraction, style detection, rotation/invisible filtering, rulings, `HasImages`) to pass T025 (research R6)
- [X] T039 [P] [US1] Implement `src/LegalAgent.PdfParser/Text/Ligatures.cs` and `src/LegalAgent.PdfParser/Stages/TextNormalizationStage.cs` (Order 200) to pass T026 (research R7)
- [X] T040 [US1] Implement `src/LegalAgent.PdfParser/Stages/LineAssemblyStage.cs` (Order 300) to pass T027
- [X] T041 [P] [US1] Implement `src/LegalAgent.PdfParser/Text/LineFingerprint.cs` and `src/LegalAgent.PdfParser/Text/Levenshtein.cs` to pass T028
- [X] T042 [P] [US1] Implement `src/LegalAgent.PdfParser/Text/PageNumberPatterns.cs` (compiled regexes, invariant culture, offset detection) to pass T029
- [X] T043 [US1] Implement `src/LegalAgent.PdfParser/Stages/ArtifactRemovalStage.cs` (Order 400; zones, odd/even grouping, Y buckets, thresholds from `ArtifactOptions`, report entries) to pass T030 (research R8)
- [X] T044 [US1] Implement `src/LegalAgent.PdfParser/Stages/ReadingOrderStage.cs` (Order 700; gutter detection, skips lines with `Role = Table`) to pass T031 (research R9)
- [X] T045 [P] [US1] Implement `src/LegalAgent.PdfParser/Text/Hyphenation.cs` (join rules + `NormalizationOptions.HyphenationExceptions`)
- [X] T046 [US1] Implement `src/LegalAgent.PdfParser/Stages/BlockAssemblyStage.cs` (Order 1000; paragraphs, cross-page continuation, `PageBreak` inlines, inline styles) to pass T032
- [X] T047 [US1] Implement minimal `src/LegalAgent.PdfParser/Stages/DocumentBuildStage.cs` (Order 1100): all blocks into `LegalDocument.Preamble`, `PageRange` per block, `SourceInfo`; sections added in US2
- [X] T048 [US1] Implement `src/LegalAgent.PdfParser/Rendering/MarkdownRenderer.cs` (`IMarkdownRenderer`; paragraphs, emphasis, page markers) to pass T033
- [X] T049 [US1] Implement facade `src/LegalAgent.PdfParser/PdfMarkdownConverter.cs` (`IPdfMarkdownConverter.ConvertAsync` + `CreateDefault(Action<PdfParserOptions>?)`: read input → open → build `PipelineContext` → `PipelineRunner` → render → `PdfConversionResult` with `IsComplete`, `Report.Elapsed`) and `src/LegalAgent.PdfParser/IPdfMarkdownConverter.cs`, `PdfConversionRequest.cs`, `ConversionProgress.cs` per contracts/public-api.md; make T034 and T035 pass

**Checkpoint**: MVP — czysty Markdown z PDF, testowalny niezależnie przez `PdfMarkdownConverter.CreateDefault()`.

---

## Phase 4: User Story 5 - Łatwa integracja w aplikacji (Priority: P1)

**Goal**: Rejestracja jednym wywołaniem w DI, opcje z konfiguracji i per wywołanie, własne etapy, anulowanie, limit czasu, postęp, bezpieczeństwo współbieżne.

**Independent Test**: `new ServiceCollection().AddLegalAgentPdfParser()` → `IPdfMarkdownConverter` konwertuje syntetyczny PDF; anulowanie w trakcie kończy się `OperationCanceledException` w czasie ≤ jednej strony.

### Tests for User Story 5 ⚠️

- [X] T050 [P] [US5] Write `tests/LegalAgent.PdfParser.Tests/Integration/DependencyInjectionTests.cs`: (a) `AddLegalAgentPdfParser()` alone resolves `IPdfMarkdownConverter` and `IMarkdownRenderer` as singletons; (b) calling it twice registers each built-in stage once; (c) `configure` delegate and `services.Configure<PdfParserOptions>(...)` both applied (e.g. `Artifacts.MinPages`); (d) invalid options → `OptionsValidationException` on first conversion; (e) `AddPdfParserStage<T>()` with `Order = 450` runs between artifact removal and footnotes (recording stage); (f) `ReplacePdfParserStage<ArtifactRemovalStage, NoOpStage>()` and `RemovePdfParserStage<TableDetectionStage>()` take effect; (g) `PdfConversionRequest.ConfigureOptions` changes only that call (global options unchanged afterwards)
- [X] T051 [P] [US5] Write `tests/LegalAgent.PdfParser.Tests/Integration/CancellationAndLimitsTests.cs`: caller token cancelled mid-document (stage that blocks on a page signals) → `OperationCanceledException` and no result; `Limits.MaxDuration` elapsed (slow test stage) → `PdfLimitExceededException(Duration)` not `OperationCanceledException`; `MaxDuration = null` disables (FR-003, FR-009b, SC-009)
- [X] T052 [P] [US5] Write `tests/LegalAgent.PdfParser.Tests/Integration/ProgressAndConcurrencyTests.cs`: `Progress` receives `(page, pageCount, stage)` for every page in non-decreasing page order (FR-072, using synchronous `IProgress` test double); 8 parallel `ConvertAsync` calls on one instance produce results identical to sequential runs

### Implementation for User Story 5

- [X] T053 [US5] Implement `src/LegalAgent.PdfParser/PdfParserServiceCollectionExtensions.cs` (`AddLegalAgentPdfParser` with `TryAdd*`, options + validator registration, `AddPdfParserStage`, `ReplacePdfParserStage`, `RemovePdfParserStage`) in namespace `Microsoft.Extensions.DependencyInjection` to pass T050
- [X] T054 [US5] Extend `src/LegalAgent.PdfParser/PdfMarkdownConverter.cs`: constructor takes `IEnumerable<IPipelineStage>`, `IOptions<PdfParserOptions>`, `IMarkdownRenderer`; per-call options via `Clone()` + `ConfigureOptions` + validation; linked CTS with `CancelAfter(MaxDuration)` distinguishing caller cancellation from timeout; progress reporting from runner; make `CreateDefault` reuse the same stage list factory — pass T050–T052

**Checkpoint**: US1 + US5 = MVP gotowe do użycia w aplikacjach.

---

## Phase 5: User Story 2 - Hierarchia nagłówków i jednostek redakcyjnych (Priority: P1)

**Goal**: Tytuł/Dział/Rozdział/Art./§ i nagłówki typograficzne jako `#`…`######`, drzewo `Section` z `Path`, przypisy na końcu sekcji.

**Independent Test**: Syntetyczna ustawa (tytuł 16 pt, „DZIAŁ I”/„Rozdział 1” + tytuły pogrubione, „Art. 1.”…„Art. 5a.”, przypisy) → golden `tests/LegalAgent.PdfParser.Tests/Integration/Expected/us2-headings.expected.md`; `Section.Path` dla Art. 3 = `["DZIAŁ I. …", "Rozdział 1. …", "Art. 3."]`.

### Tests for User Story 2 ⚠️

- [X] T055 [P] [US2] Write `tests/LegalAgent.PdfParser.Tests/Unit/Text/LegalUnitPatternsTests.cs` (FR-043, research R11): matches `KSIĘGA PIERWSZA`, `CZĘŚĆ II`, `DZIAŁ II`, `Dział IIa`, `Rozdział 3`, `Rozdział 3a`, `Oddział 2`, `Art. 5.`, `Art. 12a.`, `Art. 12¹.`, `§ 7.`, `§ 5¹.`; extracts designation and number; does not match `art. 5` mid-sentence, `zgodnie z § 7`, `Art. 5 ust. 2` without trailing period after number at line start context
- [X] T056 [P] [US2] Write `tests/LegalAgent.PdfParser.Tests/Unit/Stages/HeadingDetectionStageTests.cs`: (a) body style = mode of (size rounded 0.5 pt, bold) weighted by chars (FR-040); (b) candidate rules FR-041 — ≤ 120 chars / ≤ 2 lines, preceded by gap > 1.3× leading (`GapFactor`), not ending with `,`/`;`, and size ≥ 1.15× OR all-bold OR all-caps ≥ 3 letters OR centred (line centre within 5% of column width from column centre, both margins ≥ 10%); (c) size classes (0.5 pt tolerance) → `#`, `##`, `###`; bold-only = one below lowest enlarged class, min `##`; capped by `MaxTypographicDepth` (FR-042); (d) legal levels: only present structural types get levels from 2 without gaps; Art./§ = deepest structural + 1; no structural units → Art. at level 2 (FR-043, Clarifications Q1); (e) „Rozdział 3” + next short line same style → one heading „Rozdział 3. Ochrona konsumenta” (FR-044); (f) „Art. 5. Treść…” → heading „Art. 5.” + paragraph „Treść…” (FR-045); (g) bold word inside line is not a heading (FR-046); (h) lines with `Role` Table/Footnote never promoted (FR-047); (i) uniform typography document → only legal-pattern headings, none guessed (US2-5); (j) typographic heading „Załącznik nr 1” after last article → `##` top-level section; bold typographic heading inside a Rozdział before its first Art. → chapter level + 1; level never jumps by more than 1 relative to parent (FR-043a)
- [X] T057 [P] [US2] Write `tests/LegalAgent.PdfParser.Tests/Unit/Stages/FootnoteDetectionStageTests.cs` (FR-026): small-font block (≤ 0.9× body) at page bottom after short rule or starting with marker matching a superscript reference → footnote with original label; footnote not removed as artifact and not merged into body text; reference in body becomes `FootnoteRef`; footnote without reference → `IsOrphan` + warning `FTN001_OrphanFootnote`; footnote continued on next page joined
- [X] T058 [P] [US2] Write `tests/LegalAgent.PdfParser.Tests/Unit/Stages/DocumentBuildStageTests.cs`: section tree from heading levels (child level > parent); `Kind`, `Designation`, `Number`, `Title`, `HeadingText` filled; `Path` = ancestors' `HeadingText` + own; `Pages` spans heading to end incl. children; blocks before first heading → `Preamble`; footnote definitions attached to the smallest section where first referenced, global numbering by first reference order, repeated reference not duplicated, preamble refs → `PreambleFootnotes` (Clarifications Q5)
- [X] T059 [P] [US2] Write `tests/LegalAgent.PdfParser.Tests/Rendering/MarkdownRendererSectionTests.cs`: `#`×Level + `HeadingText`; order heading → blocks → children → section footnotes; `[^n]` refs and `[^n]: …` definitions; invariants 3 and 4 of contracts/markdown-output.md
- [X] T060 [US2] Write integration test `tests/LegalAgent.PdfParser.Tests/Integration/HeadingsIntegrationTests.cs` with the Independent Test PDF and golden `Integration/Expected/us2-headings.expected.md` (hand-written first)

### Implementation for User Story 2

- [X] T061 [P] [US2] Implement `src/LegalAgent.PdfParser/Text/LegalUnitPatterns.cs` to pass T055
- [X] T062 [US2] Implement `src/LegalAgent.PdfParser/Stages/HeadingDetectionStage.cs` (Order 900) to pass T056
- [X] T063 [US2] Implement `src/LegalAgent.PdfParser/Stages/FootnoteDetectionStage.cs` (Order 500) to pass T057
- [X] T064 [US2] Extend `src/LegalAgent.PdfParser/Stages/DocumentBuildStage.cs` with section tree, `Path`, page ranges and footnote placement to pass T058
- [X] T065 [US2] Extend `src/LegalAgent.PdfParser/Rendering/MarkdownRenderer.cs` with headings and footnotes to pass T059 and T060

**Checkpoint**: Wszystkie historyjki P1 działają.

---

## Phase 6: User Story 3 - Zachowanie list wyliczeniowych (Priority: P2)

**Goal**: Wyliczenia prawne i listy punktowane jako zagnieżdżone listy Markdown z dosłownymi oznaczeniami.

**Independent Test**: Syntetyczny artykuł z ust. 1–2, pkt `1)`–`3)`, lit. `a)`–`b)` w pkt 2, tiret, częścią wspólną i punktem przechodzącym przez stronę + lista `•` → golden `Integration/Expected/us3-lists.expected.md`.

### Tests for User Story 3 ⚠️

- [X] T066 [P] [US3] Write `tests/LegalAgent.PdfParser.Tests/Unit/Text/ListLabelPatternsTests.cs` (FR-050): classifies `•`, `▪`, `◦`, `‣`, `–`, `—`, `-`, `*`, Symbol/Wingdings bullet glyphs (→ `Bullet`), `1)`, `1a)`, `a)`, `aa)`, `1.`, `IV.`, `iv)`, `1.2.3.`; requires following space/gap and text
- [X] T067 [P] [US3] Write `tests/LegalAgent.PdfParser.Tests/Unit/Stages/ListDetectionStageTests.cs`: `N.` is a list only in a sequence of ≥ 2 increasing numbers at same indent or as article ustęp; `2024 r. weszła…` stays a paragraph (FR-051); nesting by label X with 1.5 pt tolerance and legal hierarchy ust. → pkt → lit. → tiret (FR-052); wrapped lines aligned to item text are continuations, also across page break (FR-053); trailing common part aligned to parent text becomes paragraph after list (FR-054); `Lists.Enabled = false` disables
- [X] T068 [P] [US3] Write `tests/LegalAgent.PdfParser.Tests/Rendering/MarkdownRendererListTests.cs`: `- 1\) …`, nested 2-space indent `  - a\) …`, bullet char dropped `- karta debetowa`, tiret `- – …`, inline page marker inside item, blank lines between list and paragraphs
- [X] T069 [US3] Write integration test `tests/LegalAgent.PdfParser.Tests/Integration/ListsIntegrationTests.cs` with golden `Integration/Expected/us3-lists.expected.md`

### Implementation for User Story 3

- [X] T070 [P] [US3] Implement `src/LegalAgent.PdfParser/Text/ListLabelPatterns.cs` to pass T066
- [X] T071 [US3] Implement `src/LegalAgent.PdfParser/Stages/ListDetectionStage.cs` (Order 800) to pass T067
- [X] T072 [US3] Extend `src/LegalAgent.PdfParser/Stages/BlockAssemblyStage.cs` to build `ListBlock`/`ListItem` (children, continuations, common part) from list roles
- [X] T073 [US3] Extend `src/LegalAgent.PdfParser/Rendering/MarkdownRenderer.cs` with lists to pass T068 and T069

---

## Phase 7: User Story 4 - Czytelne tabele opłat (Priority: P2)

**Goal**: Tabele opłat jako tabele GFM lub (fallback) wiersze „ | ”; kwoty zawsze w wierszu z nazwą usługi.

**Independent Test**: Syntetyczna 2-stronicowa taryfa (kolumny Usługa/Opłata/Częstotliwość, komórki 2-liniowe, powtórzony nagłówek na stronie 2, wariant z siatką i bez) + układ niejednoznaczny → golden `Integration/Expected/us4-tables.expected.md`.

### Tests for User Story 4 ⚠️

- [X] T074 [P] [US4] Write `tests/LegalAgent.PdfParser.Tests/Unit/Layout/ColumnClusteringTests.cs`: left edges within 3% page width cluster into one column band; deterministic band ordering; ruling lines snap band boundaries
- [X] T075 [P] [US4] Write `tests/LegalAgent.PdfParser.Tests/Unit/Stages/TableDetectionStageTests.cs`: segments separated by gap > 2× mean space are cells (FR-060); ≥ 3 consecutive lines with ≥ 2 aligned segments → table region (FR-061); slight baseline jitter still one row; partial-column line with gap ≤ 1.2× table leading (`RowMergeGapFactor`) merged into previous row unless a horizontal ruling separates them (FR-062); bold top row → header; amounts `0,00 zł`, `1,5%`, `min. 10 zł` stay in one cell (FR-063); varying column count → `IsFallback = true` + warning `TBL001_AmbiguousGrid` (FR-064); continuation on next page with same bands merged and repeated header dropped (FR-065); cell text spanning two bands assigned to first column with `ColumnSpan` and no text lost (FR-066); `Tables.Enabled = false` disables
- [X] T076 [P] [US4] Write `tests/LegalAgent.PdfParser.Tests/Rendering/MarkdownRendererTableTests.cs`: GFM table with header and `| --- |` separator, `|` escaped in cells, no page markers inside table and next page marker after it; fallback rows as separate paragraphs with ` \| ` separators (no GFM table produced)
- [X] T077 [US4] Write integration test `tests/LegalAgent.PdfParser.Tests/Integration/TablesIntegrationTests.cs` with golden `Integration/Expected/us4-tables.expected.md`; also assert two-column text page next to a table is not split into columns (FR-031 vs tables)

### Implementation for User Story 4

- [X] T078 [P] [US4] Implement `src/LegalAgent.PdfParser/Layout/ColumnClustering.cs` to pass T074
- [X] T079 [US4] Implement `src/LegalAgent.PdfParser/Stages/TableDetectionStage.cs` (Order 600; sets `Role = Table`, builds table `LayoutBlock` with bands, rows, header, fallback flag, cross-page merge) to pass T075 (research R10)
- [X] T080 [US4] Extend `src/LegalAgent.PdfParser/Stages/DocumentBuildStage.cs` to emit `TableBlock` (`Header`, `Rows`, `ColumnCount`, `IsFallback`, `Pages`) and report `TableCount`/`FallbackTableCount`
- [X] T081 [US4] Extend `src/LegalAgent.PdfParser/Rendering/MarkdownRenderer.cs` with GFM and fallback tables to pass T076 and T077

---

## Phase 8: User Story 6 - Diagnostyka jakości konwersji (Priority: P3)

**Goal**: Raport kompletny, strony pominięte jawnie, tryb wyniku częściowego.

**Independent Test**: PDF ze stroną 7 będącą samym obrazem → raport `PDF001_NoTextLayer` dla strony 7, Markdown zawiera `<!-- page 7 skipped: no-text-layer -->`, `IsComplete == false`.

### Tests for User Story 6 ⚠️

- [X] T082 [P] [US6] Add fixture `tests/LegalAgent.PdfParser.Tests/Corpus/errors/broken-page.pdf` (3 pages, page 2 content stream invalid so PdfPig throws when reading it) and document its production in `Corpus/errors/README.md`
- [X] T083 [P] [US6] Write `tests/LegalAgent.PdfParser.Tests/Integration/ErrorHandlingTests.cs`: image-only page → `SkippedPageBlock(NoTextLayer)`, warning `PDF001_NoTextLayer` with page, `IsComplete = false`, no exception (FR-071); fully blank page skipped silently; document with no text at all → `PdfNoTextException`; `broken-page.pdf` default → `PdfPageReadException(PageNumber = 2)`; with `AllowPartialResult = true` → `SkippedPageBlock(PageReadError)`, `IsComplete = false`, `Report.SkippedPages` lists page 2 with reason; all pages failing → exception even in partial mode; limit exceptions unaffected by partial mode (FR-009a, FR-009b)
- [X] T084 [P] [US6] Write `tests/LegalAgent.PdfParser.Tests/Unit/Pipeline/ConversionReportTests.cs` (FR-070): `PageCount`, `HeadingCounts` keys ascending, `ListCount`, `TableCount`, `FallbackTableCount`, `FootnoteCount`, `DroppedTextCount`, warnings sorted by page then code; unmapped glyphs → `TXT001_UnmappedGlyphs`; images present → `IMG001_ImagesIgnored`
- [X] T085 [P] [US6] Write `tests/LegalAgent.PdfParser.Tests/Rendering/MarkdownRendererSkippedPageTests.cs`: `<!-- page N skipped: no-text-layer -->` and `<!-- page N skipped: read-error -->` rendered at the page position

### Implementation for User Story 6

- [X] T086 [US6] Extend `src/LegalAgent.PdfParser/Stages/PageExtractionStage.cs` with per-page try/catch, `AllowPartialResult` handling, image-only vs blank detection, unmapped glyph detection, warnings
- [X] T087 [US6] Extend `src/LegalAgent.PdfParser/Pipeline/ReportBuilder.cs` and `src/LegalAgent.PdfParser/Stages/DocumentBuildStage.cs` (`SkippedPageBlock`, counts, `PdfNoTextException` when no text in whole document) to pass T083 and T084
- [X] T088 [US6] Extend `src/LegalAgent.PdfParser/Rendering/MarkdownRenderer.cs` with skipped-page markers to pass T085

---

## Phase 9: Aplikacja CLI (cross-cutting, konstytucja: biblioteka + aplikacja)

**Purpose**: Cienka konsola wg [contracts/cli.md](./contracts/cli.md).

- [X] T089 Write `tests/LegalAgent.PdfParser.Tests/Integration/CliTests.cs` running the CLI entry point in-process (`Program.RunAsync(args, stdout, stderr, env)`): stdout output without `-o`; `-o` writes file atomically (temp + move, existing file replaced only on success); `--report` writes camelCase JSON; `--no-page-markers`, `--allow-partial` honoured; env `PDFPARSER__Limits__MaxPages=1` applied; exit codes 0/2/3/4/5/6/1 per contract; Polish messages on stderr
- [X] T090 Implement `src/LegalAgent.PdfParser.Cli/Program.cs` (manual arg parsing, `--help`, `--version`, env → `PdfParserOptions` via Configuration.Binder, DI registration, Ctrl+C → cancellation → exit 130) to pass T089

---

## Phase 10: Polish & Cross-Cutting Concerns (korpus, metryki SC, dokumentacja)

- [X] T091 [P] Download ≥ 4 consolidated acts (tekst ujednolicony) from ISAP into `tests/LegalAgent.PdfParser.Tests/Corpus/acts/`: Prawo bankowe (działy, rozdziały, przypisy), ustawa o usługach płatniczych (> 100 stron), ustawa o kredycie konsumenckim, ustawa o prawach konsumenta; record source URL and download date in `Corpus/acts/SOURCES.md`
- [X] T092 [P] Implement `tests/LegalAgent.PdfParser.Tests/Fixtures/BankingCorpusGenerator.cs` producing ≥ 4 synthetic banking documents into `Corpus/banking/` (regulamin with registry footer and § structure; 2-page tariff table with grid; tariff without grid with multi-line cells; two-column regulamin with bullet lists) — generic names, no real bank branding
- [X] T093 Create `*.expected.md` for every corpus document in `Corpus/acts/` and `Corpus/banking/` by running conversion with `UPDATE_GOLDEN=1`, then manually reviewing and correcting each against the PDF (record reviewer notes in `Corpus/REVIEW.md`)
- [X] T094 Write `tests/LegalAgent.PdfParser.Tests/Corpus/GoldenTests.cs` (theory over all corpus PDFs → `GoldenFile.AssertMatches`) and optional `PrivateCorpusTests` reading `LEGALAGENT_PRIVATE_CORPUS` directory, skipped when unset
- [X] T095 Write `tests/LegalAgent.PdfParser.Tests/Corpus/QualityMetricsTests.cs` computing from model vs expected: page-number removal 100% and running header/footer removal ≥ 99% (SC-001); word recall ≥ 99.5% (SC-002); legal-unit heading recall ≥ 95%, false headings ≤ 2% (SC-003); list label+level accuracy ≥ 95% (SC-004); amounts in same row as service name 100%, full GFM tables ≥ 80% (SC-005)
- [X] T096 Tune default thresholds in `src/LegalAgent.PdfParser/Options/*.cs` until T094–T095 pass; document every changed default and reason in `specs/001-legal-pdf-parser/research.md` (append section „Dostrojenie”) and update data-model.md §2
- [X] T097 [P] Write `tests/LegalAgent.PdfParser.Tests/PerformanceTests.cs` (`[Trait("Category","Performance")]`): 100-page corpus act converts in < 10 s; memory growth roughly linear (200 vs 100 pages peak working set ratio < 2.5) (SC-007)
- [X] T098 [P] Write `README.md` at repo root: purpose, prerequisites (.NET SDK per `global.json`, runtime 9.0.x GA, ICU on Linux, no invariant globalization), library usage (3-line DI snippet from quickstart.md §4), options reference table, custom pipeline stages, CLI usage and exit codes, running tests incl. `UPDATE_GOLDEN`, `LEGALAGENT_PRIVATE_CORPUS`, Performance category (SC-008, constitution VII)
- [X] T099 [P] Complete XML-doc on all public types in `src/LegalAgent.PdfParser/` (build with `GenerateDocumentationFile` must produce no CS1591 warnings)
- [ ] T100 Run all validation steps from `specs/001-legal-pdf-parser/quickstart.md` (build, test, CLI scenarios, error scenarios) on Windows and confirm CI green on Linux; fix discrepancies — **Windows: wykonane 2026-10-07 (build 0 ostrzeżeń, 758 testów zielonych + 1 pominięty, scenariusze CLI i błędów zgodne); Linux CI: czeka na wypchnięcie gałęzi (decyzja właściciela)**

---

## Phase 11: Schematy kroków „Kolejność działań” (FR-067, US4, dodane 2026-10-08)

**Purpose**: Dwukolumnowe schematy kroków regulaminów (szare pola z nazwami kroków bez obramowania, strzałki-obrazki,
wyjaśnienie z punktorami po prawej) dziś „rozsypują się”: linie obu kolumn przeplatają się, a wiersz „Kolejność działań
Wyjaśnienie” wychodzi jako `###`. Wynik: sekwencja „**nazwa kroku**” (oryginalny tekst, T108) + wyjaśnienie jako akapity/listy (spec FR-067,
US4 scenariusz 6, Clarifications 2026-10-08). Weryfikacja na `Corpus/private/mbank-regulamin-pdp.pdf` (16 schematów,
strony 9, 27–39, 46–47).

- [X] T101 [US4] Extend `tests/LegalAgent.PdfParser.Tests/Fixtures/SyntheticPdfBuilder.cs` `FilledRect` with an optional gray level and write tests in `tests/LegalAgent.PdfParser.Tests/Unit/Stages/PageExtractionStageTests.cs`: a filled non-white rectangle is recorded in `LayoutPage.FilledAreas` (top-down coordinates); white fills, page-sized backgrounds and thin rules (kept as rulings) are not
- [X] T102 [US4] Add `LayoutPage.FilledAreas` and record filled areas in `src/LegalAgent.PdfParser/Stages/PageExtractionStage.cs` to pass T101
- [X] T103 [US4] Write `tests/LegalAgent.PdfParser.Tests/Unit/Stages/StepSequenceStageTests.cs`: two or more stacked step boxes with text to their right → title lines get `LineRole.StepTitle` + `LayoutAnnotations.StepNumber` (1, 2, 3), lines spanning both columns are split at the box edge, page lines reordered to title₁, explanation₁, title₂…; explanation lines annotated with the step and the explanation column bounds; the column-name row above the first box → `LineRole.Artifact`; per-line shading inside a box ignored; an empty box on the next page continues the previous step (no number) and the repeated column-name row is skipped; text between boxes starts a new scheme (numbering restarts); no detection for a single box, a box wider than 50% of the page, a box without text to its right, or when `TableOptions.DetectStepSequences = false`
- [X] T104 [US4] Implement `src/LegalAgent.PdfParser/Stages/StepSequenceStage.cs` (`StageOrder.StepSequence = 550`; registration in `BuiltInStages` moved to T106, together with block assembly of step titles), `LineRole.StepTitle`, `LayoutAnnotations.StepNumber`/`StepIndex`, option `TableOptions.DetectStepSequences` (default true) to pass T103
- [X] T105 [US4] Write tests: `BlockAssemblyStageTests` — consecutive `StepTitle` lines of one step form one paragraph „Krok N: nazwa” in bold (hyphenation resolved), the explanation becomes ordinary paragraphs and lists; `ReadingOrderStageTests`/`TableDetectionStageTests` — scheme lines are neither a text column nor a table; `Integration/TablesIntegrationTests` — synthetic 2-page scheme (header row, 3 boxes with vertically centred titles, bullets on the right, continuation box with repeated header) renders as „**Krok 1: …**” … with no `#` heading, no table and no interleaved lines
- [X] T106 [US4] Register `StepSequenceStage` in `BuiltInStages`, implement step titles in `src/LegalAgent.PdfParser/Stages/BlockAssemblyStage.cs` and the exclusions in `ReadingOrderStage`/`TableDetectionStage` to pass T105
- [X] T107 [US4] Verify on `Corpus/private/mbank-regulamin-pdp.pdf` (all 16 schemes; fix defects test-first), run the full suite and goldens (no unintended changes in `Corpus/acts`, `Corpus/banking`), update `contracts/markdown-output.md`, `data-model.md`, README options table and the handoff in `plan.md`

- [X] T108 [US4] Step names render as their original text only (no added „Krok N:”, owner's correction 2026-10-08): update `BlockAssemblyStageTests` and `Integration/StepSchemesIntegrationTests` (red), then drop the prefix in `src/LegalAgent.PdfParser/Stages/BlockAssemblyStage.cs`; update `contracts/markdown-output.md`, README, `plan.md`, `Corpus/REVIEW.md`; re-verify on `Corpus/private/mbank-regulamin-pdp.pdf`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Ph1)** → **Foundational (Ph2)** → blokuje wszystkie historyjki.
- **US1 (Ph3)**: po Ph2. Dostarcza fasadę i etapy bazowe — wymagane przez wszystkie kolejne historyjki.
- **US5 (Ph4)**: po US1 (rozszerza fasadę).
- **US2 (Ph5)**: po US1 (korzysta z linii, bloków, renderera). Niezależny od US5.
- **US3 (Ph6)** i **US4 (Ph7)**: po US1; wzajemnie niezależne; zalecane po US2 (testy integracyjne zawierają nagłówki), ale nie wymagane.
- **US6 (Ph8)**: po US1.
- **CLI (Ph9)**: po US5 (DI) i US6 (kody wyjścia dla wyniku niepełnego).
- **Polish (Ph10)**: po wszystkich historyjkach.

### Graf

```text
Ph1 → Ph2 → US1 ─┬─► US5 ──────────┐
                 ├─► US2 ─┬► US3 ──┤
                 │        └► US4 ──┼─► CLI ─► Polish
                 └─► US6 ──────────┘
```

### Within Each User Story

- Testy (⚠️) najpierw i czerwone → implementacja → refaktoryzacja.
- Wzorce/tekst (`Text/*`) → etapy → `DocumentBuildStage` → renderer → test integracyjny zielony.
- Pliki współdzielone (`MarkdownRenderer.cs`, `DocumentBuildStage.cs`, `BlockAssemblyStage.cs`, `PageExtractionStage.cs`) rozszerzane sekwencyjnie — przy równoległej pracy nad US3/US4/US6 koordynować scalanie.

### Parallel Opportunities

- Ph1: T005, T006, T007 równolegle po T004.
- Ph2: T008, T010, T011, T012, T013, T016, T017, T020 równolegle.
- US1: wszystkie testy T022–T033 równolegle; implementacje T036, T039, T041, T042, T045 równolegle.
- US5: T050–T052 równolegle.
- US2: T055–T059 równolegle; T061 równolegle z testami innych etapów.
- US3 i US4 jako całe fazy równolegle (różne etapy; uwaga na renderer).
- Ph10: T091, T092, T097, T098, T099 równolegle.

---

## Parallel Example: User Story 1

```text
# Red — testy jednocześnie:
T022 PdfInputReaderTests      T025 PageExtractionStageTests   T028 FingerprintAndSimilarityTests
T023 PdfDocumentOpenerTests   T026 TextNormalizationStageTests T029 PageNumberPatternsTests
T027 LineAssemblyStageTests   T030 ArtifactRemovalStageTests  T031 ReadingOrderStageTests
T032 BlockAssemblyStageTests  T033 MarkdownRendererParagraphTests

# Green — niezależne pliki jednocześnie:
T036 PdfInputReader   T039 Ligatures + TextNormalizationStage
T041 LineFingerprint + Levenshtein   T042 PageNumberPatterns   T045 Hyphenation
```

## Parallel Example: User Story 4

```text
T074 ColumnClusteringTests   T075 TableDetectionStageTests   T076 MarkdownRendererTableTests
→ T078 ColumnClustering (równolegle z przygotowaniem T077)
```

---

## Implementation Strategy

### MVP First (US1 + US5)

1. Ph1 + Ph2.
2. US1 → **STOP i walidacja**: czysty Markdown z syntetycznej „ustawy” bez artefaktów, determinizm.
3. US5 → biblioteka gotowa do wpięcia w aplikację (DI, anulowanie, limity).

### Incremental Delivery

1. MVP (US1 + US5) → użyteczny tekst do RAG bez struktury.
2. + US2 → struktura artykułów, `Path`, przypisy → chunking po jednostkach prawnych.
3. + US3 / US4 → listy i tabele opłat.
4. + US6 → pełna diagnostyka i tryb częściowy.
5. + CLI + korpus + metryki SC + README → gotowe do wydania 1.0.0.

### Notes

- [P] = różne pliki, brak zależności od niezakończonych zadań.
- Commit po każdym zadaniu lub parze test/implementacja.
- Każdy checkpoint = historyjka testowalna niezależnie.
- Ryzyko platformy (.NET 9 EOL 2026-11-10, brak lokalnego runtime 9.0 GA) — patrz plan.md, Constitution Check; rozstrzygnąć przed T001.
