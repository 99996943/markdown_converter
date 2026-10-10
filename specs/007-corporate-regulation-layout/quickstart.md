# Quickstart: walidacja spec 007

## Wymagania

- .NET SDK z `global.json`, solucja buduje się (`dotnet build LegalAgent.slnx -c Release`).
- Prywatny korpus właściciela w `tests/LegalAgent.PdfParser.Tests/Corpus/private` (poza git) z 15 dokumentami mBanku
  dodanymi w kroku przygotowania (goldeny obecnego wyniku jako punkt odniesienia) i plikiem z listą dokumentów
  „układu etykiet” (D-A…D-D).

## 1. Testy jednostkowe i repliki stron (offline, CI)

```bash
dotnet test tests/LegalAgent.PdfParser.Tests -- --filter-class "*ListLabelPatternsTests"
dotnet test tests/LegalAgent.PdfParser.Tests -- --filter-class "*HangingLabelLayoutTests"
dotnet test tests/LegalAgent.PdfParser.Tests -- --filter-class "*ParagraphUnitHeadingTests"
dotnet test tests/LegalAgent.PdfParser.Tests -- --filter-class "*GlossaryLayoutTests"
```

Oczekiwane: repliki dają listy z etykietami „1/”, „a/”, nagłówki „§ N” o poziom niżej niż rozdział, słowniczek jako
lista definicji; kontrola (tabela danych z „1/” w pierwszej kolumnie) zostaje tabelą GFM.

## 2. Pełna kontrola regresji przed każdym commitem (FR-534)

```bash
dotnet test LegalAgent.slnx --filter "Category!=Performance"
LEGALAGENT_PRIVATE_CORPUS="C:/GIT/markdown_converter/tests/LegalAgent.PdfParser.Tests/Corpus/private" \
  LEGALAGENT_CORPUS_REPORT=1 dotnet test tests/LegalAgent.PdfParser.Tests
dotnet run --project src/LegalAgent.Corpus.Cli -c Release -- refresh
dotnet run --project src/LegalAgent.Corpus.Cli -c Release -- verify
LEGALAGENT_CORPUS_FULL=1 dotnet test tests/LegalAgent.Corpus.Tests
```

Oczekiwane: goldeny `Corpus/acts` i `Corpus/banking` bez zmian; prywatny korpus — zmiany tylko w D-A…D-D (i
zatwierdzone przez właściciela w 5 detalicznych, jeśli wystąpią); `verify` bez różnic albo różnice przejrzane i
zacommitowane; miary `CorpusFull` nie gorsze.

## 3. Miary sukcesu (SC-080…SC-087)

Tabela miar drukowana przez test prywatnego korpusu przy `LEGALAGENT_CORPUS_REPORT=1`:

| Dokument | TBL001 ≤ | Wiersze „ \| ” ≤ | „§ N” jako tekst |
|---|---|---|---|
| D-A | 13 | 114 | 0 |
| D-B | 2 | 14 | 0 |
| D-C | 6 | 55 | 0 |
| D-D | 8 | 67 | 0 |

Pozostałe 11 dokumentów: żadna miara nie rośnie względem punktu odniesienia.

## 4. Przebieg FAQ na dokumentach dla firm (SC-083)

```powershell
.\mBank.FaqGenerator.exe --output downloads-corp --url <D-A> --url <D-B> --url <D-C> --url <D-D> --url <inny>
```

Oczekiwane: brak ostrzeżeń „jednostka „§ N” nie występuje w dokumencie”.
