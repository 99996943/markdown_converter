# Quickstart: walidacja LegalAgent.PdfParser

Przewodnik uruchomienia i sprawdzenia, że funkcjonalność działa end-to-end. Szczegóły API:
[contracts/public-api.md](./contracts/public-api.md); format wyniku:
[contracts/markdown-output.md](./contracts/markdown-output.md); CLI: [contracts/cli.md](./contracts/cli.md).

## Wymagania

- .NET SDK 10.0.x (wg `global.json`) **oraz runtime .NET 9.0.x GA** (`dotnet --list-runtimes`
  musi pokazać `Microsoft.NETCore.App 9.0.*` bez sufiksu `preview`).
- Linux: biblioteka ICU (Ubuntu/Debian — zwykle obecna; Alpine: `apk add icu-libs`). Nie ustawiać
  `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1`.

## 1. Budowanie i testy (Linux i Windows)

```bash
dotnet build LegalAgent.slnx -c Release
dotnet test LegalAgent.slnx -c Release
```

Oczekiwane: wszystkie testy zielone, w tym:

| Grupa testów | Co potwierdza |
|--------------|---------------|
| `Unit/*Stage*Tests` | każda heurystyka FR-010 – FR-066 na syntetycznych PDF |
| `Rendering/MarkdownInvariantsTests` | niezmienniki z markdown-output.md |
| `Corpus/GoldenTests` | wynik = `*.expected.md` dla każdego dokumentu korpusu |
| `Corpus/QualityMetricsTests` | progi SC-001 – SC-005 |
| `DeterminismTests` | SC-006: dwa uruchomienia → identyczny wynik |
| `Integration/DependencyInjectionTests` | US5: rejestracja, nadpisanie opcji, własny etap, anulowanie |
| `Integration/ErrorHandlingTests` | FR-009, FR-009a, FR-009b, FR-071: typy wyjątków i flaga `IsComplete` |
| `PerformanceTests` (kategoria `Performance`) | SC-007: 100 stron < 10 s |

## 2. Konwersja przez CLI

```bash
dotnet run --project src/LegalAgent.PdfParser.Cli -c Release -- \
  convert tests/LegalAgent.PdfParser.Tests/Corpus/acts/<ustawa>.pdf \
  -o /tmp/ustawa.md --report /tmp/ustawa.report.json
echo $?   # 0
```

Sprawdzenie ręczne `/tmp/ustawa.md`:

- brak linii „Dziennik Ustaw – N – Poz. …” i numerów stron (`grep -c "Dziennik Ustaw" /tmp/ustawa.md` → 0 poza tytułem, jeśli występuje w treści);
- nagłówki: `grep -E "^#{1,6} " /tmp/ustawa.md | head` → tytuł `#`, rozdziały `##`, artykuły `###`;
- znaczniki stron: `grep -c "<!-- page:" /tmp/ustawa.md` ≈ liczba stron;
- przypisy `[^1]: …` bezpośrednio po treści artykułu z odnośnikiem;
- raport: `removedArtifacts` zawiera wzorzec nagłówka Dziennika Ustaw z liczbą wystąpień ≈ liczba stron.

Wariant bez znaczników: dodać `--no-page-markers` → brak `<!-- page:`.

## 3. Scenariusze błędów (CLI)

| Polecenie | Oczekiwany kod / komunikat |
|-----------|----------------------------|
| `convert README.md` | 3, „plik nie jest dokumentem PDF” |
| `convert <zaszyfrowany>.pdf` (z `Corpus/errors/`) | 3, „dokument chroniony hasłem” |
| `convert <uszkodzona-strona>.pdf` | 4, numer strony |
| `convert <uszkodzona-strona>.pdf --allow-partial` | 6, plik zapisany, znacznik `<!-- page N skipped: read-error -->` |
| `PDFPARSER__Limits__MaxPages=1 … convert <ustawa>.pdf` | 5, „przekroczono limit liczby stron: 1 (dokument: N)” |

## 4. Integracja w aplikacji (SC-008)

W nowej aplikacji konsolowej z `Microsoft.Extensions.DependencyInjection` — 3 linie konfiguracji:

```csharp
var services = new ServiceCollection().AddLegalAgentPdfParser();
var converter = services.BuildServiceProvider().GetRequiredService<IPdfMarkdownConverter>();
var result = await converter.ConvertAsync(File.OpenRead("dokument.pdf"));
```

Oczekiwane: `result.IsComplete == true`, `result.Markdown` niepusty,
`result.Document.Sections[*].Path` zawiera ścieżki typu `["Rozdział 1. …", "Art. 1."]`.

## 5. Linux w CI

Workflow `.github/workflows/ci.yml` (ubuntu-latest) wykonuje kroki z sekcji 1. Zielony przebieg
potwierdza zgodność z Linuxem i deterministyczność między systemami (golden files tworzone na
Windows są porównywane na Linuxie).
