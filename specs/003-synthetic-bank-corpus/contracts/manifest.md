# Kontrakt: `corpus/manifest.json`

JSON, UTF-8 bez BOM, wcięcie 2 spacje, klucze camelCase (angielskie), wartości wyliczeniowe jak nazwy
katalogów (polskie, ASCII). Właściwości o wartości `null` są pomijane. Kolejność kluczy stała (jak w
tabelach niżej). Manifest nie zawiera ról ani uprawnień (FR-140). Wersja schematu: `schemaVersion: 1`
(zmiana niezgodna = +1).

## Korzeń

```json
{
  "schemaVersion": 1,
  "run": { … },
  "documents": [ … ]
}
```

### `run` (FR-143)
| Klucz | Typ | Opis |
|-------|-----|------|
| `seed` | number | ziarno |
| `referenceDate` | string `rrrr-mm-dd` | data odniesienia statusu |
| `parameters` | object | pełne `RunParameters` (jak `przebieg.json`, w tym `parserOptions`, bez ścieżek bezwzględnych) |
| `parserVersion` | string | wersja `LegalAgent.PdfParser` użyta do Markdown |
| `generatorVersion` | string | wersja `LegalAgent.Corpus` |
| `contentHash` | string | SHA-256 plików `zrodla/` (porządek ordinal ścieżek) |
| `repeatedWordShare` | number? | tylko przebieg nieścisły (FR-103b): udział słów bloków powtórzonych między dokumentami w całym przebiegu (0–1, 3 miejsca) |

Brak znacznika czasu uruchomienia (powtarzalność).

## `documents[]` — pola wspólne

| Klucz | Typ | Opis |
|-------|-----|------|
| `id` | string | `REG-03`, `REG-03-w1`, `ZAT-TAR-POL-02`, `dz-u-2025-644-aml` |
| `type` | id typu z `typy.yaml` (obecnie `regulaminy` \| `taryfy` \| `procedury`) albo `akty` | typ dokumentu |
| `title` | string | tytuł z okładki |
| `designation` | string? | oznaczenie z treści (`BP/REG/03`); akty: oznaczenie publikatora |
| `version` | number? | numer wersji |
| `validFrom`, `validTo` | string? | daty obowiązywania z treści |
| `status` | `obowiazujacy` \| `nieaktualny` | względem `run.referenceDate`; dla zatrutych — status **rzeczywisty** (np. dokument nieaktualny przedstawiony jako obowiązujący ma `nieaktualny`) |
| `previousVersion` | string? | `id` wersji poprzedniej |
| `pdf`, `markdown` | string | ścieżki względne od `corpus/`, separator `/` |
| `chunks` | string | (spec 004) plik fragmentów `<id>.chunks.jsonl` obok Markdown, ścieżka względna od `corpus/`; format: `specs/004-document-chunking/contracts/chunks-json.md` |
| `pages` | number | liczba stron PDF |
| `template`, `layout`, `seed` | string, string, number | tylko dokumenty syntetyczne |
| `sharedWordShare`, `repeatedWordShare` | number | udział słów bloków wspólnych / powtórzonych (0–1, 3 miejsca) |
| `changes` | `VersionChange[]`? | zmiany względem `previousVersion` |
| `contradictions` | `Contradiction[]`? | sprzeczności z innymi dokumentami korpusu |
| `poison` | `PoisonInfo`? | tylko dokumenty zatrute |
| `source` | `ActInfo`? | tylko akty |
| `notes` | string? | uwaga opisowa (np. „dokument nieaktualny — brak następcy w korpusie”) |

### `VersionChange` / `Contradiction` (FR-141)
| Klucz | Opis |
|-------|------|
| `with` | `id` dokumentu powiązanego (tylko `Contradiction`) |
| `unit` | jednostka: `§ 12 ust. 3 pkt 2`, `poz. 4.7`, `krok 5.2`, `metryczka` |
| `page` | strona w tym dokumencie |
| `fact` | identyfikator faktu, jeśli zmiana/sprzeczność dotyczy faktu |
| `before`, `after` | brzmienie lub wartość: przed/po (`VersionChange`) lub tutaj/tam (`Contradiction`: `this`, `other`) |

### `PoisonInfo` (FR-142)
| Klucz | Opis |
|-------|------|
| `kind` | rodzaj problemu (= nazwa katalogu) |
| `imitates` | `id` dokumentu podrabianego / sprzecznego |
| `description` | opis zatrucia |
| `places[]` | `{ page, unit, element, text, goal? }` — `element`: `akapit` \| `przypis` \| `komorka-tabeli` \| `metryczka` \| `okladka` \| `ramka`; `text`: dosłowny tekst zatrucia, taki jak w PDF; `goal`: cel z FR-132a dla `polecenia-dla-ai` |

### `ActInfo` (FR-151)
`journal` („Dz. U. 2025 poz. 644”), `consolidatedTextDate`, `url`, `downloadedOn`, `notes`.

## Gwarancje dla aplikacji RAG

- Każdy `pdf`/`markdown`/`chunks` istnieje; każde `previousVersion`, `with`, `imitates` wskazuje istniejący `id`.
- Każdy `places[].text` występuje dosłownie w PDF dokumentu (warstwa tekstu) i — po zwykłym
  odescapowaniu Markdown — w jego Markdown (SC-023).
- Dla jednego `designation` wersje mają rozłączne, ciągłe okresy `validFrom`–`validTo`.
