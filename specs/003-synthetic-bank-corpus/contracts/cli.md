# Kontrakt: aplikacja `LegalAgent.Corpus.Cli`

Uruchomienie z katalogu głównego repozytorium:

```text
dotnet run --project src/LegalAgent.Corpus.Cli -c Release -- <polecenie> [opcje]
```

## Polecenia

| Polecenie | Działanie |
|-----------|-----------|
| `generate` | Planuje, składa PDF, konwertuje biblioteką do Markdown, zapisuje manifest; sprząta nieaktualne pliki w zarządzanych katalogach (FR-100, FR-107, FR-108). |
| `refresh` | Bez składania PDF: konwertuje wszystkie PDF z manifestu (także akty) do Markdown i przepisuje manifest (wersja biblioteki, liczba stron). Akty z `zrodla/akty.yaml` dopisywane/aktualizowane; `akty/ZRODLA.md` odtwarzany. |
| `verify` | Odtwarza w pamięci wszystko, co zrobiłby `generate` (+ `refresh` dla aktów), i porównuje z plikami na dysku; niczego nie zapisuje. Wypisuje różniące się pliki. |
| `--help`, `--version` | Pomoc / wersja. |

## Opcje (`generate`, `verify`; nadpisują `--params`)

| Opcja | Pole `RunParameters` | Przykład |
|-------|----------------------|----------|
| `--params <plik>` | cały zestaw (domyślnie `corpus/przebieg.json`, jeśli istnieje) | `--params corpus/przebieg.json` |
| `--types <lista>` | `Types` | `--types regulaminy,procedury` |
| `--count <n>` | `DocumentsPerType` | `--count 15` |
| `--pages <min>-<max>` | `Pages` | `--pages 40-50` |
| `--seed <n>` | `Seed` | `--seed 42` |
| `--reference-date <rrrr-mm-dd>` | `ReferenceDate` | |
| `--versioned <procent>` | `VersionedShare` | `--versioned 30` |
| `--outdated <n>` | `OutdatedPerType` | |
| `--contradictions <n>[,<między-typami>]` | `ContradictionPairsPerType`, `CrossTypeContradictionPairs` | `--contradictions 1,1` |
| `--poison <rodzaj>=<n>[,…]` lub `--poison none` | `Poison` | `--poison polecenia-dla-ai=3,podszywanie=2` |
| `--no-strict-uniqueness` | `StrictUniqueness = false` | dla dużych przebiegów |
| `--out <katalog>` | `OutputDirectory` | `--out /tmp/korpus` |
| `--content <katalog>` | `ContentDirectory` | |
| `--truth <katalog>` | — | zapis prawdy referencyjnej (diagnostyka, JSON na dokument) |
| `--save-params` | — | zapisuje efektywne parametry do `<out>/przebieg.json` |

## Kody wyjścia

| Kod | Znaczenie |
|-----|-----------|
| 0 | Sukces (`verify`: brak różnic) |
| 1 | `verify`: różnice między dyskiem a odtworzeniem (lista na stderr) |
| 2 | Błędne parametry lub błąd w plikach źródłowych (plik, ścieżka YAML, komunikat) |
| 3 | Zakres stron nieosiągalny / brak bloków / naruszenie unikalności lub udziału bloków wspólnych (dokument, szablon) |
| 4 | Nazwa zabroniona w treści (FR-105; wskazanie bloku źródłowego) |
| 5 | Błąd konwersji biblioteką lub konwersja niekompletna (dokument) |
| 6 | Błąd wejścia/wyjścia (ścieżka) |

Błędy nie pozostawiają częściowego korpusu: pliki zapisywane atomowo, sprzątanie dopiero po udanym
przebiegu (zasady III–IV). Komunikaty po polsku na stderr; postęp (dokument N/M) na stdout.
