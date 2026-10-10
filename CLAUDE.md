# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

- **`LegalAgent.PdfParser`**: a .NET 9 library that converts PDFs into a structured document model and Markdown, for a downstream RAG chunker that splits on headings. Inputs are Polish legal acts (ISAP / Dziennik Ustaw) and bank regulations.
- **`LegalAgent.PdfParser.Cli`**: a thin CLI over the library. The executable is `legalagent-pdf` (`convert`, `chunk`).
- **`LegalAgent.Chunking`**: splits a parser result into chunks with metadata for a RAG app (spec 004). In-memory models
  only (no file I/O); JSON Lines contract in `specs/004-document-chunking/contracts/chunks-json.md`.
- **`LegalAgent.Corpus`** and **`LegalAgent.Corpus.Cli`**: a deterministic generator of a synthetic Polish bank corpus („Bank Przykładowy S.A.”), committed in `corpus/`.
- **`LegalAgent.Downloads`** and **`mBank.FaqGenerator`** (spec 005): the library downloads a list of PDF addresses in
  parallel (allowed hosts, manual redirects, time/size limits, `.part` + atomic move, `manifest.json`, cleanup); the
  console app asks for 5 regulation URLs (or takes `--url` ×5 / `Download:Urls` from `appsettings.json` and
  `FAQGEN__…` variables) and writes them to `./downloads` (git-ignored — real bank documents). The library knows no
  hosts; `mbank.pl` and the count 5 live in the app.
- **`LegalAgent.Faq`** (spec 006): `Conversion/DocumentSetConverter` writes `<name>.md` next to each PDF (parser
  defaults, atomic, stale `*.md` removed after 5/5); `FaqGenerator` asks the model in two steps — candidates per
  document D1…Dn in turn, then one selection of 10 — and validates every JSON response (`FaqResponseParser`,
  `FaqResponseValidator`, `UnitMatcher`); `FaqMarkdownRenderer` writes the OKF file with a hand-written YAML header.
  Depends only on `Microsoft.SemanticKernel.Abstractions`; no „mBank”, „Azure” or file name in it. The app stages
  after a full download: conversion → `CheckInput` → key (`KeyPrompt`: stars in the console, or the next line of
  redirected stdin) → `FaqGenerator` through `ChatServiceFactory` (Azure OpenAI connector, no retries, per-request
  timeout, strict JSON schema) → `faq/FAQ_mBank.md` (not git-ignored). Exit codes 5 conversion, 6 service/too long,
  7 rejected response. `scripts/azure/create-openai.sh` creates the resource group, account and deployment
  idempotently and never reads the key.
- **The API key is never configuration**: no option, no environment variable, `AzureOpenAI:ApiKey` is rejected; FAQ
  messages go through `SecretRedactor`.

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
dotnet run --project src/LegalAgent.PdfParser.Cli -c Release -- chunk in.pdf -o out.chunks.jsonl --id REG-06 --designation BP/REG/06
# perf tests of one project: dotnet test tests/LegalAgent.Chunking.Tests -- --filter-class "LegalAgent.Chunking.Tests.PerformanceTests"

# corpus generator (defaults to corpus/ and corpus/przebieg.json)
dotnet run --project src/LegalAgent.Corpus.Cli -c Release -- generate   # plan + typeset PDFs + convert + manifest
dotnet run --project src/LegalAgent.Corpus.Cli -c Release -- refresh    # re-convert existing PDFs only (after parser changes)
dotnet run --project src/LegalAgent.Corpus.Cli -c Release -- verify     # rebuild in memory, compare with disk (CI step)
dotnet run --project src/LegalAgent.Corpus.Cli -c Release -- check --template <id>

# download → conversion → FAQ (prompts for 5 URLs without --url; exit codes 0–7/130 in specs/006-faq-generation/contracts/cli.md)
dotnet run --project src/mBank.FaqGenerator -c Release -- --url <a> --url <b> --url <c> --url <d> --url <e> [--output <dir>]
```

The download tests never touch the network: `FakeHttpHandler` (in `tests/LegalAgent.Downloads.Tests/Fakes/`, linked
into the app tests) scripts responses, and app tests run `Program.RunAsync` through `AppHarness` with their own
`appsettings.json`.

The FAQ tests never touch Azure. `tests/LegalAgent.Faq.Tests/Fakes/` (linked into the app tests) has
`FakeChatCompletionService` (scripted answers, a `Fallback` that answers any request validly, call log), `FaqJson`
and `TestPdfs` (synthetic regulations). `AppHarness` passes an `AppHost` with `FakeKeyInput`, a fixed clock, the fake
model, or the real connector over `ModelHttpHandler` (`UseConnector`). `AzureScriptTests` run the script with a fake
`az` first on `PATH` and skip without `bash`.

```bash
# FAQ run (endpoint in appsettings.Local.json or FAQGEN__AzureOpenAI__Endpoint; key typed or piped)
az cognitiveservices account keys list -g rg-faqgen -n <name> --query key1 -o tsv \
  | dotnet run --project src/mBank.FaqGenerator -c Release -- --url <a> … --url <e> [--faq-output <dir>]
scripts/azure/create-openai.sh [--model gpt-5.4-mini --model-version 2026-03-17]   # Bash; Git Bash on Windows
```

Environment variables used by the tests:
- **`UPDATE_GOLDEN=1`**: rewrites `*.expected.md` goldens and the chunk goldens `tests/LegalAgent.Chunking.Tests/Golden/*.chunks.jsonl`. On a mismatch, tests write `*.actual.md` / `*.actual.jsonl` (git-ignored).
- **`LEGALAGENT_PRIVATE_CORPUS=<dir>`**: runs the owner's private bank PDFs against their goldens. These files are not in git. Locally the directory is `tests/LegalAgent.PdfParser.Tests/Corpus/private`.
- **`LEGALAGENT_CORPUS_FULL=1`**: enables the `CorpusFull` category, which runs metrics over the whole committed corpus and checks that its Markdown is up to date. Plain `dotnet test` only checks a fixed sample.
- **`LEGALAGENT_CORPUS_REPORT`**: writes per-document measurement tables.

Parser options can be overridden through `PDFPARSER__<Group>__<Field>` env vars in the CLI; chunking options through `CHUNKING__<Field>` (e.g. `CHUNKING__MaxChunkLength`).

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

## Chunking architecture

`DocumentChunker` (`src/LegalAgent.Chunking/`) works on the parser's `LegalDocument`, never on Markdown, and has no
structure heuristics of its own — structure errors are fixed in the parser.
- `UnitCollector`: units are the preamble and the own content of each section; a section with neither content nor
  subsections is a heading-only unit; the title is metadata only.
- `UnitSplitter`: units longer than `MaxChunkLength` (default 2000) are split into atoms (paragraph, table row, list
  item with nested items as separate atoms, unreferenced footnote) packed greedily; every part starts with the unit
  heading, table parts with the header row. `FragmentRenderer` renders each part through the parser's
  `MarkdownRenderer` without page markers; `PageTracker` gives part pages (`TableRow.Page` from the parser).
- `UnitKeyBuilder`: `unitKey` = designation shared by versions + shortest unique segment path (`BP/REG/06 | § 30`),
  so the same unit has the same key in every version; `ChunkIdBuilder`: `<docId>_<hash16(unitKey)>_<part>`.
- Metadata values meant for the model are English (`type` = regulation/tariff/procedure/act, `status` =
  in-force/outdated, preamble key segment `~preamble`); document text stays as printed.

## Corpus generator architecture

`CorpusGenerator` pipeline:
1. **Content** — `corpus/zrodla/` holds YAML templates (`szablony/`), blocks with sentence variants and parameters (`bloki/`), and facts with value history (`fakty/`); `Content/` loads them.
2. **`Planning/`** — chooses documents, versions, contradictions and poisoned documents from `RunParameters` and a seed (`DeterministicRandom`).
3. **`Composition/`** — turns templates and blocks into elements.
4. **`Typesetting/`** — lays the elements out with one `LayoutStyle` per layout (`jedna-kolumna`, `dwie-kolumny`, `tabela-dokument`, `taryfa-*`, `procedura`), through `PageWriter`. While setting text, it records the **truth** (`Truth/DocumentTruth`: words, headings, list items, tables).
5. **`Pdf/SyntheticPdfBuilder`** — writes the PDF; `PdfIdNormalizer` makes it byte-reproducible.
6. **Parser** — converts each PDF to Markdown, and **`LegalAgent.Chunking`** writes `<id>.chunks.jsonl` next to it
   (metadata from the manifest entry; English type names from `nazwa-en` in `typy.yaml`, statuses translated in code;
   the manifest keeps its Polish values). Earlier versions of a document share the optional blocks and their order with
   the latest version (`DocumentPlan.SeriesId`), so paragraph numbers and unit keys match across versions (FR-120).
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
