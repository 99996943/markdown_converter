# Quickstart: weryfikacja spec 012

Wymagania: SDK z `global.json`, runtime .NET 9; prywatny korpus w `tests/LegalAgent.PdfParser.Tests/Corpus/private`
(ścieżka bezwzględna w zmiennej).

## 1. Testy jednostkowe, repliki, renderer

```bash
dotnet test tests/LegalAgent.PdfParser.Tests -- --filter-method "*Footnote*"
dotnet test tests/LegalAgent.Chunking.Tests -- --filter-method "*Footnote*"
```

Oczekiwane: replika regulaminu (tabela z „²¹”–„²⁴”, akapit z „⁷”) daje `TAK[^21]` … i `[^7]`; replika aktu z dwiema
seriami „1)” daje `[^1)]` i `[^1)-2]`; przypis bez etykiety → identyfikator zastępczy i `FTN002`.

## 2. Goldeny (zmiana do akceptacji właściciela)

```bash
dotnet test LegalAgent.slnx --filter "Category!=Performance"      # na red/green: goldeny padają, *.actual.md
```

Diff `*.expected.md` ↔ `*.actual.md` po normalizacji identyfikatorów (zamiana `[^id]` na numer z 1.3.1) MUSI być
pusty (SC-133). Dopiero po akceptacji właściciela: `UPDATE_GOLDEN=1` i commit goldenów.

## 3. Prywatny korpus i bramki

```bash
LEGALAGENT_PRIVATE_CORPUS="$(pwd)/tests/LegalAgent.PdfParser.Tests/Corpus/private" \
  dotnet test tests/LegalAgent.PdfParser.Tests
```

Oczekiwane: `RegulationNumbers_HaveNoExcess` przechodzi bez zmian `number-check.txt`; FTN001 aktów jak w bramce
spec 011 (2, 5, 24, 10, 6, 11).

## 4. Dokument z przykładu

```bash
dotnet run --project src/LegalAgent.PdfParser.Cli -c Release -- convert \
  "publish/mBank.FaqGenerator-win-x64/downloads/regulamin_kredytu_gotowkowego_dla_osob_fizycznych_i_klientow_private_banking_w_ramach_bankowosci_detalicznej_mbanku_s.a.pdf" \
  -o out/kg.md
```

Oczekiwane: tabela s. 21 z odnośnikami o etykietach z PDF-u (21–24), definicje `[^1]`…`[^25]` z etykietami PDF-u.

## 5. Podgląd GFM

Wkleić fragment z `[^21]`, `[^1)]`, `[^*)]`, `[^1)-2]` do podglądu VS Code i do opisu PR/gista na GitHubie: każdy
odnośnik prowadzi do swojej definicji.

## 6. Korpus syntetyczny i publish/

```bash
dotnet run --project src/LegalAgent.Corpus.Cli -c Release -- generate   # wersja 1.4.0: generate, nie samo refresh
dotnet run --project src/LegalAgent.Corpus.Cli -c Release -- verify
```

Oczekiwane: w `corpus/` zmieniają się tylko identyfikatory przypisów w `*.md`/`*.chunks.jsonl` i wersje w manifeście.
Potem `publish/` jak w spec 011 T016 (NuGet 1.4.0, exe self-contained skompresowany).
