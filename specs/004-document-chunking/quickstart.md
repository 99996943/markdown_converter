# Quickstart: walidacja podziału na fragmenty (spec 004)

Wymagania: SDK z `global.json`, runtime .NET 9; repozytorium z zacommitowanym `corpus/`.

## 1. Build i testy

```bash
dotnet build LegalAgent.slnx -c Release
dotnet test LegalAgent.slnx --filter "Category!=Performance"
```

Oczekiwane: zielone testy `LegalAgent.Chunking.Tests` (jednostkowe, pliki wzorcowe, determinizm) i
test manifestu w `LegalAgent.Corpus.Tests` (FR-273); pliki wzorcowe parsera bez zmian (SC-046).

Pełny korpus (pokrycie słów, limit, unikalność identyfikatorów — SC-040 – SC-044):

```bash
LEGALAGENT_CORPUS_FULL=1 dotnet test LegalAgent.slnx --filter "Category!=Performance"
```

## 2. CLI — jeden PDF

```bash
dotnet run --project src/LegalAgent.PdfParser.Cli -c Release -- \
  chunk corpus/regulaminy/REG-06.pdf -o /tmp/REG-06.chunks.jsonl \
  --id REG-06 --designation BP/REG/06 --type regulation --doc-version 3 --valid-from 2026-06-01
```

Oczekiwane: kod 0, jedna linia JSON na fragment zgodna z `contracts/chunks-json.md`; fragment z
`"citation":"§ 30"` ma `unitKey` `BP/REG/06 | § 30`. Błędne `--valid-from 2025-13-01` → kod 2.

## 3. Korpus

```bash
dotnet run --project src/LegalAgent.Corpus.Cli -c Release -- refresh
dotnet run --project src/LegalAgent.Corpus.Cli -c Release -- verify
```

Oczekiwane: obok każdego `corpus/**/<id>.md` jest `<id>.chunks.jsonl`, wpis manifestu ma `chunks`;
`verify` kończy się kodem 0; drugi `refresh` nie zmienia żadnego pliku (`git status` czysty). Ręczna
zmiana jednej linii pliku fragmentów → `verify` kod 1 ze wskazaniem pliku.

## 4. Porównanie wersji (ręcznie)

W `corpus/regulaminy/REG-06.chunks.jsonl` i `REG-06-w2.chunks.jsonl` fragmenty z
`"unitKey":"BP/REG/06 | § 30"` mają różne `id`, a ich `content` różni się wartością z
`manifest.json` → `changes` (§ 30 ust. 3: „9:00” → „8:30”).

## 5. Serwis (szkic użycia)

```csharp
services.AddLegalAgentChunking(o => o.MaxChunkLength = 2000);
// …
ChunkedDocument doc = await chunker.ChunkAsync(pdfStream, new DocumentMetadata("REG-06") { Designation = "BP/REG/06" }, cancellationToken: ct);
string jsonl = ChunkJson.ToJsonLines(doc);
```
