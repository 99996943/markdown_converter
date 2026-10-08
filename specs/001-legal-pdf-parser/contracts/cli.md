# Contract: aplikacja `LegalAgent.PdfParser.Cli`

Cienka warstwa nad biblioteką (konstytucja: argumenty, konfiguracja, kody wyjścia). Brak logiki
konwersji w aplikacji.

## Składnia

```text
legalagent-pdf convert <wejście.pdf> [-o|--output <wyjście.md>] [--report <raport.json>]
                       [--no-page-markers] [--allow-partial]
legalagent-pdf --help | --version
```

- Bez `-o` Markdown trafia na stdout (UTF-8, LF).
- `--report` zapisuje `ConversionReport` jako JSON (camelCase, `Elapsed` jako sekundy).
- Komunikaty błędów i ostrzeżeń → stderr, po polsku, z kodem ostrzeżenia i numerem strony.
- Opcje heurystyk mogą pochodzić ze zmiennych środowiskowych `PDFPARSER__<Grupa>__<Pole>`
  (np. `PDFPARSER__Limits__MaxPages=5000`) — zgodnie z zasadą V.
- Zapis pliku wyjściowego atomowy (plik tymczasowy + zamiana), aby ponowne uruchomienie nie
  zostawiało uszkodzonego wyniku (zasada III).

## Kody wyjścia

| Kod | Znaczenie |
|-----|-----------|
| 0 | Sukces, wynik kompletny |
| 2 | Błędne argumenty / plik wejściowy nie istnieje |
| 3 | `InvalidPdfException` / `PdfEncryptedException` / `PdfNoTextException` |
| 4 | `PdfPageReadException` |
| 5 | `PdfLimitExceededException` |
| 6 | Sukces, ale wynik niepełny (`IsComplete == false`) — plik zapisany |
| 130 | Przerwane (Ctrl+C) |
| 1 | Nieoczekiwany błąd |
