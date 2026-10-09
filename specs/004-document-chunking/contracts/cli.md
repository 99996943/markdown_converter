# Kontrakt: polecenie `chunk` w `legalagent-pdf` i pliki fragmentów korpusu

## `legalagent-pdf chunk`

```text
legalagent-pdf chunk <plik.pdf> -o <wyjście.jsonl> [opcje]
dotnet run --project src/LegalAgent.PdfParser.Cli -c Release -- chunk in.pdf -o out.chunks.jsonl --designation BP/REG/05
```

| Opcja | Pole | Domyślnie |
|-------|------|-----------|
| `-o`, `--output <plik>` | plik JSONL (wymagany) | — |
| `--id <id>` | `DocumentId` | nazwa pliku PDF bez rozszerzenia, znaki spoza `[A-Za-z0-9._-]` → `-` |
| `--designation <tekst>` | `Designation` | brak (= id) |
| `--type <tekst>` | `Type` | brak |
| `--title <tekst>` | `Title` | brak (tytuł z parsera) |
| `--doc-version <n>` | `Version` | brak |
| `--valid-from <rrrr-mm-dd>`, `--valid-to <rrrr-mm-dd>` | daty | brak |
| `--status <tekst>` | `Status` | brak |
| `--previous-version <id>` | `PreviousVersion` | brak |
| `--max-length <n>` | `ChunkingOptions.MaxChunkLength` | 2000 |
| `--allow-partial` | jak w `convert` | |

Opcje parsera — jak w `convert` (`PDFPARSER__<Grupa>__<Pole>`). Opcje fragmentów można też nadpisać
zmiennymi `CHUNKING__<Pole>` (np. `CHUNKING__MaxChunkLength=1500`); argument ma pierwszeństwo.

Zapis atomowy (plik tymczasowy + przeniesienie). Ostrzeżenia parsera na stderr jak w `convert`; na
stdout podsumowanie: liczba fragmentów, liczba przekraczających limit.

Kody wyjścia — jak `convert` (contracts/cli.md spec 001): 0 sukces; 2 błędne argumenty, brak pliku,
błędne metadane (np. `--doc-version 0`, zła data, niedozwolony `--id`) lub opcje; 3/4/5 błędy PDF; 6
wynik niepełny (plik zapisany); 130 przerwanie; 1 błąd nieoczekiwany.

## Pliki fragmentów korpusu

- `corpus/<typ>/<id>.chunks.jsonl` obok `<id>.md` dla każdego wpisu manifestu (regulaminy, taryfy,
  procedury, zatrute, akty).
- Metadane z wpisu manifestu: `id`, `designation`, `type`, `title`, `version`, `validFrom`, `validTo`,
  `status`, `previousVersion`; `type` i `status` po angielsku (`regulation`/`tariff`/`procedure`/`act`,
  `in-force`/`outdated`); opcje fragmentów domyślne.
- Wpis manifestu ma pole `chunks` ze ścieżką pliku.
- `generate` i `refresh` zapisują pliki; `verify` porównuje je z odtworzeniem (kod 1 przy różnicy,
  lista plików na stderr); sprzątanie usuwa pliki fragmentów dokumentów, których już nie ma.
