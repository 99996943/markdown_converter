# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

- **`LegalAgent.PdfParser`**: a .NET 9 library that converts PDFs into a structured document model and Markdown, for a downstream RAG chunker that splits on headings. Inputs are Polish legal acts (ISAP / Dziennik Ustaw) and bank regulations.
- **`LegalAgent.PdfParser.Cli`**: a thin CLI over the library. The executable is `legalagent-pdf`.
- **`LegalAgent.Corpus`** and **`LegalAgent.Corpus.Cli`**: a deterministic generator of a synthetic Polish bank corpus („Bank Przykładowy S.A.”), committed in `corpus/`.

The owner communicates in Polish. Specs, the README, `corpus/README.md` and the corpus content are in Polish; code, comments and commit messages are in English.

## Commands

The SDK version comes from `global.json`; .NET 9 runtime is required. Tests use xUnit v3 on Microsoft Testing Platform.

```bash
dotnet build LegalAgent.slnx -c Release
dotnet test LegalAgent.slnx --filter "Category!=Performance"        # what CI runs (perf tests are load-sensitive locally)
dotnet test LegalAgent.slnx --filter "Category=Performance"

# single test / class (MTP syntax, after "--")
dotnet test tests/LegalAgent.PdfParser.Tests -- --filter-method "*NameFragment*"
dotnet test tests/LegalAgent.Corpus.Tests -- --filter-class "*SpecialLayoutsTests"
# note: `-- --filter-trait "Category!=Performance"` silently runs 0 tests — use --filter on the solution instead

# parser CLI
dotnet run --project src/LegalAgent.PdfParser.Cli -c Release -- convert in.pdf -o out.md --report out.report.json

# corpus generator (defaults to corpus/ and corpus/przebieg.json)
dotnet run --project src/LegalAgent.Corpus.Cli -c Release -- generate   # plan + typeset PDFs + convert + manifest
dotnet run --project src/LegalAgent.Corpus.Cli -c Release -- refresh    # re-convert existing PDFs only (after parser changes)
dotnet run --project src/LegalAgent.Corpus.Cli -c Release -- verify     # rebuild in memory, compare with disk (CI step)
dotnet run --project src/LegalAgent.Corpus.Cli -c Release -- check --template <id>
```

Environment variables used by the tests:
- **`UPDATE_GOLDEN=1`**: rewrites `*.expected.md` goldens. On a mismatch, tests write `*.actual.md`.
- **`LEGALAGENT_PRIVATE_CORPUS=<dir>`**: runs the owner's private bank PDFs against their goldens. These files are not in git. Locally the directory is `tests/LegalAgent.PdfParser.Tests/Corpus/private`.
- **`LEGALAGENT_CORPUS_FULL=1`**: enables the `CorpusFull` category, which runs metrics over the whole committed corpus and checks that its Markdown is up to date. Plain `dotnet test` only checks a fixed sample.
- **`LEGALAGENT_CORPUS_REPORT`**: writes per-document measurement tables.

Parser options can be overridden through `PDFPARSER__<Group>__<Field>` env vars in the CLI.

## Parser architecture

`PdfMarkdownConverter` runs an ordered list of `IPipelineStage`s over a shared `PipelineContext`. The order values are in `Pipeline/StageOrder.cs`:

PageExtraction 100 → TextNormalization 200 → LineAssembly 300 → ArtifactRemoval 400 → FootnoteDetection 500 → StepSequence 550 → TableDocument 560 → TableDetection 600 → ReadingOrder 700 → ListDetection 800 → HeadingDetection 900 → BlockAssembly 1000 → DocumentBuild 1100.

**How stages talk to each other.** Stages mostly communicate by mutating `LayoutPage.Lines` and leaving data on them:
- `LayoutLine.Role` (Unknown / Heading / Table / Artifact …);
- `HeadingInfo`;
- string annotations from `Layout/LayoutAnnotations.cs`, e.g. `tabledoc.index`, step index, table index, column left/right.

A later stage usually skips lines that an earlier stage claimed. For example, `TableDetection` and `ReadingOrder` skip lines annotated as belonging to a table-document. When you change one stage, check how the downstream stages read those annotations.

**Text extraction is custom.** The parser does not use PdfPig's `DocumentLayoutAnalysis`; the reasons are in `specs/001-legal-pdf-parser/research.md` R3. Words, lines, segments, columns, tables and headings are all built from letters, rulings and images. This keeps per-letter style (`**bold**` fragments) and keeps wide gaps as table-cell boundaries.

**Headings.** `HeadingDetectionStage` handles three kinds:
- legal units (Dział / Rozdział / Art. / §), via `Text/LegalUnitPatterns`;
- typographic headings (size classes, bold, caps, centring);
- table-document section names.

`AssignLevels` gives structural kinds consecutive levels from 2, puts units one level below the deepest structural kind, and caps typographic levels. The resulting level hierarchy is a contract invariant (no gaps).

**Rendering.** `Rendering/` turns the section tree into CommonMark + GFM. The output contract is `specs/001-legal-pdf-parser/contracts/markdown-output.md`, plus the addendum in spec 002. It covers page markers `<!-- page: N -->`, footnotes `[^n]`, escaped list labels (`- 1\)`), GFM tables, and a fallback table format (`a \| b`) with warning TBL001.

**Output must contain only text from the PDF.** Never synthesize words such as labels or „Krok N:”. Structure is expressed only through Markdown formatting.

## Corpus generator architecture

`CorpusGenerator` pipeline:
1. **Content** — `corpus/zrodla/` holds YAML templates (`szablony/`), blocks with sentence variants and parameters (`bloki/`), and facts with value history (`fakty/`); `Content/` loads them.
2. **`Planning/`** — chooses documents, versions, contradictions and poisoned documents from `RunParameters` and a seed (`DeterministicRandom`).
3. **`Composition/`** — turns templates and blocks into elements.
4. **`Typesetting/`** — lays the elements out with one `LayoutStyle` per layout (`jedna-kolumna`, `dwie-kolumny`, `tabela-dokument`, `taryfa-*`, `procedura`), through `PageWriter`. While setting text, it records the **truth** (`Truth/DocumentTruth`: words, headings, list items, tables).
5. **`Pdf/SyntheticPdfBuilder`** — writes the PDF; `PdfIdNormalizer` makes it byte-reproducible.
6. **Parser** — converts each PDF to Markdown.
7. **`Validation/CorpusChecks`** — compares the parser output against the truth.
8. **`Manifest/`** — writes `corpus/manifest.json`.

A typesetting change that alters what is printed must also update the recorded truth, or the corpus metrics fail. After any generator or parser change, run `generate` and/or `refresh` and commit the regenerated `corpus/` files. CI runs `verify`, which fails if `corpus/` is stale.

## Workflow rules

- **Spec Kit + TDD.** Features go through `specs/NNN-*/` (spec → plan → tasks → implement). The constitution is `.specify/memory/constitution.md`.
  - Every behaviour change is a **separate red commit** (a failing test) followed by a **green commit** (the fix).
  - Each spec's `plan.md` ends with a handoff section, „Stan prac i przekazanie”. Keep it and `tasks.md` updated; new fixes are added as tasks like T089x.
- **Parser bugs found on real PDFs.** Reproduce the page as a synthetic replica in a test, using `SyntheticPdfBuilder` (in the tests' `Fixtures/`) or `TableSheet`. Before committing, run the full parser suite with `LEGALAGENT_PRIVATE_CORPUS` set.
- **Goldens.** Parser goldens (`tests/LegalAgent.PdfParser.Tests/Corpus/**/*.expected.md`) must not change without the owner's explicit approval (FR-163). Report the diff and record it in the spec handoff.
- **Line endings.** Goldens are checked out with LF (`.gitattributes`). Write regexes with `\n`, not literal newlines.
- **Owner's private files in the repo root.** These are untracked bank PDF/MD files („Akt prawny …”, „Regulamin … mBanku …”, `mbank-reg3.md`).
  - Stage explicit paths only. Never `git add -A`, and avoid `git stash -u`.
  - No real bank names may appear in the corpus.
- **Check the current branch before committing** (`git branch --show-current`). The owner may switch branches or merge PRs between sessions; never commit to `main`.
- **Commit trailer.** Commit messages end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
