# Quickstart: walidacja spec 009

Wymagania: SDK z `global.json`, runtime .NET 9; dla kroków prywatnych katalog
`tests/LegalAgent.PdfParser.Tests/Corpus/private` z PDF-ami, goldenami, `number-check.txt` i `baseline-009/`.

## 1. Testy offline (CI)

```bash
dotnet build LegalAgent.slnx -c Release
dotnet test LegalAgent.slnx --filter "Category!=Performance"
dotnet test tests/LegalAgent.PdfParser.Tests -- --filter-class "*NumberCheckTests"
dotnet test tests/LegalAgent.PdfParser.Tests -- --filter-method "*Superscript*"
```

Oczekiwane: wszystko zielone; test repliki s. 8 daje „art. 66¹ § 1 do § 3 oraz art. 68²”, test stopki — cztery
ciągłe wiersze w kolejności PDF i osobny napis logo.

## 2. Prywatny korpus i bramka liczb

```bash
LEGALAGENT_PRIVATE_CORPUS=tests/LegalAgent.PdfParser.Tests/Corpus/private LEGALAGENT_CORPUS_REPORT=1 \
  dotnet test tests/LegalAgent.PdfParser.Tests -- --filter-class "*PrivateCorpusTests"
```

Oczekiwane po US1 i US3: `RegulationNumbers_HaveNoExcess` zielony; w `layout-metrics.md` kolumna `Liczby +` = 0 dla
15 regulaminów z listy, firm-1 raportuje `150.000`, `230.000`, `320.000`, `450.000`; zbiory słów vs `baseline-009`
— zmiany tylko w corp-5 (indeks) i stopkach corp-1, 2, 3, 5, 6 (zgodnie z kontraktem
[number-check.md](./contracts/number-check.md)).

Kontrola na kodzie sprzed US1 (krok red): bramka pada z `mbank-corp-5: nadmiarowe 661×1, 682×1` i liczbami z
przeplecionej stopki corp-2.

## 3. Goldeny chronione (wąska zgoda FR-674)

```bash
git diff --word-diff=porcelain tests/LegalAgent.PdfParser.Tests/Corpus/acts corpus/akty \
  tests/LegalAgent.Chunking.Tests/Golden
```

Oczekiwane: każda zmiana to „art. N” + cyfry → „art. N” + znaki indeksu (`385⁵`, `556⁵`), zniknięcie fałszywego
`[^n]` w tym miejscu i wynikające przenumerowanie przypisów; nic poza tym. `Corpus/banking`, ind-*, reg*,
regulamin-pdp — bez zmian.

## 4. Korpus syntetyczny i wydanie

```bash
dotnet run --project src/LegalAgent.Corpus.Cli -c Release -- refresh
dotnet run --project src/LegalAgent.Corpus.Cli -c Release -- verify
LEGALAGENT_CORPUS_FULL=1 dotnet test LegalAgent.slnx --filter "Category=CorpusFull"
dotnet test LegalAgent.slnx --filter "Category=Performance"
```

Oczekiwane: `verify` czysty, manifest z wersją 1.2.1; miary `CorpusFull` bez pogorszenia; SC-007 spełnione. Na
koniec przebudowa `publish/` (jak w spec 007/008).
