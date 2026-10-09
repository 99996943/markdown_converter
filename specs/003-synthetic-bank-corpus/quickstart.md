# Quickstart / walidacja: syntetyczny korpus banku (spec 003)

**Feature**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)

Wymagania: .NET SDK z `global.json`; polecenia z katalogu głównego repozytorium (Linux lub Windows,
Git Bash). Sieć potrzebna wyłącznie do jednorazowego pobrania aktów (krok 6).

## 1. Build i testy (próbka korpusu)

```bash
dotnet build LegalAgent.slnx -c Release
dotnet test LegalAgent.slnx -c Release --no-build --filter "Category!=Performance"
```

Oczekiwane: wszystkie testy zielone; testy `CorpusFull` zgłoszone jako pominięte (brak
`LEGALAGENT_CORPUS_FULL`); istniejące `GoldenTests` parsera bez zmian (SC-029).

## 2. Pełny korpus (jak w CI)

```bash
LEGALAGENT_CORPUS_FULL=1 dotnet test LegalAgent.slnx -c Release --no-build --filter "Category=CorpusFull"
```

Oczekiwane: SC-020 – SC-028, SC-031 dla wszystkich dokumentów; komunikat błędu wskazuje dokument i
jednostkę.

## 3. Odtworzenie korpusu jednym poleceniem (US1, SC-021)

```bash
dotnet run --project src/LegalAgent.Corpus.Cli -c Release -- generate --params corpus/przebieg.json
git status --porcelain corpus/          # oczekiwane: brak zmian
dotnet run --project src/LegalAgent.Corpus.Cli -c Release -- verify       # kod 0
```

Oczekiwane: drugi przebieg nie zmienia żadnego pliku; czas < 10 min (SC-021); 10 dokumentów bazowych
na typ + wersje wcześniejsze, każdy PDF 20–30 stron (SC-020).

## 4. Prawdziwość manifestu (US3, US4)

Ręcznie, dla jednego dokumentu z wersjami i jednego zatrutego:

1. W `corpus/manifest.json` znaleźć wpis z `previousVersion`; otworzyć oba PDF — okładki mają kolejne
   wersje i ciągłe daty; `changes[].before/after` widoczne na wskazanej stronie i jednostce.
2. Znaleźć wpis z `poison.kind = "polecenia-dla-ai"`; tekst `places[0].text` występuje dosłownie w
   Markdown dokumentu w jednostce `places[0].unit`; dokument `imitates` istnieje i wygląda tak samo
   (okładka, nagłówki, stopki).

## 5. Rozbudowa bez kodu (US5, SC-030)

```bash
dotnet run --project src/LegalAgent.Corpus.Cli -c Release -- generate \
  --types procedury --count 15 --pages 40-50 --seed 7 --poison none --no-strict-uniqueness \
  --out /tmp/korpus-proba
```

Oczekiwane: 15 procedur po 40–50 stron + Markdown + manifest w `/tmp/korpus-proba`; manifest podaje
`repeatedWordShare`. Następnie dopisać blok w `corpus/zrodla/bloki/procedury/<temat>.yaml` wg
`corpus/README.md`, uruchomić ponownie z tym samym ziarnem — blok pojawia się w części dokumentów.

## 6. Akty prawne (US6)

Jednorazowo (opisane w `corpus/README.md`), np.:

```bash
curl --fail --max-time 60 -o corpus/akty/dz-u-2025-644-aml.pdf https://dziennikustaw.gov.pl/D2025000064401.pdf
head -c 5 corpus/akty/dz-u-2025-644-aml.pdf     # %PDF-
dotnet run --project src/LegalAgent.Corpus.Cli -c Release -- refresh
```

Oczekiwane: `corpus/akty/` ma 10 aktów (PDF + MD), `ZRODLA.md` i wpisy manifestu z `source.url`.

## 7. Poprawki biblioteki (US2, FR-163)

Dla układu źle obsłużonego: czerwony test na minimalnym PDF z `SyntheticPdfBuilder` w
`tests/LegalAgent.PdfParser.Tests` (commit red) → poprawka (commit green) → `generate`/`refresh`
odświeża Markdown korpusu → diff `corpus/**/*.md` przeglądany razem z poprawką; goldeny parsera bez
zmian lub zmiana zatwierdzona przez właściciela.
