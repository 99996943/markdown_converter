# Quickstart: walidacja tabeli-dokumentu (spec 002)

**Feature**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)

Wymagania i budowanie jak w `specs/001-legal-pdf-parser/quickstart.md` (.NET SDK z `global.json`,
runtime 9.0, na Linuksie ICU). Polecenia z katalogu głównego repozytorium.

## 0. Punkt odniesienia przed implementacją (jednorazowo, lokalnie)

Prywatne wyniki sprzed zmiany są punktem odniesienia dla braku regresji (SC-016). Przed pierwszym
commitem implementacji:

```bash
mkdir -p tests/LegalAgent.PdfParser.Tests/Corpus/private/baseline
for f in mbank-regulamin-pdp mbank-reg1 mbank-reg2 mbank-reg3; do
  dotnet run --project src/LegalAgent.PdfParser.Cli -c Release -- \
    convert tests/LegalAgent.PdfParser.Tests/Corpus/private/$f.pdf \
    -o tests/LegalAgent.PdfParser.Tests/Corpus/private/baseline/$f.md
done
```

(katalog `Corpus/private` jest w `.gitignore` — nic z tego nie trafia do repozytorium)

## 1. Testy automatyczne

```bash
dotnet build LegalAgent.slnx -c Release
dotnet test LegalAgent.slnx -c Release
```

| Grupa testów | Co potwierdza |
|--------------|---------------|
| `Unit/Stages/TableDocumentStageTests` | ramka i granice wierszy (podkreślenia linków ignorowane), kryteria FR-080 a–e, wiersz nazw kolumn (także brak powtórzeń), rozcinanie linii, nazwa wieloliniowa i przerwana stroną, kolejność linii, pierwszeństwo schematu kroków |
| `Unit/Stages/HeadingDetection*` | FR-086/087 (brak nagłówków typograficznych od początku tabeli-dokumentu), FR-088 (podpis grafiki), FR-093 („Obowiązuje od …” jako akapit) |
| `Unit/Stages/BlockAssembly*`, `ListDetection*` | koniec akapitu „zmieściłoby się”, granica pogrubienia, punktor „o” w innej czcionce |
| `Unit/Text/HyphenationTests` | FR-094 — łącznik w adresie zostaje |
| `Corpus/GoldenTests` | `banking/regulamin-promocji-tabela.expected.md` (nowy), tabela definicji w dokumencie negatywnym nadal GFM, **wszystkie dotychczasowe `*.expected.md` bez zmian** (SC-016) |
| `Corpus/QualityMetricsTests` | SC-010 – SC-015 na syntetycznej tabeli-dokumencie (słowa, nazwy sekcji, 0 fałszywych nagłówków, 0 tabel, ciągłość przez strony, zagnieżdżenie list) |
| `Rendering/MarkdownInvariantsTests` | niezmienniki 1–9 (`contracts/markdown-output.md`) |
| `DeterminismTests` + CI (ubuntu) | SC-017 |

Korpus prywatny (opcjonalnie):

```bash
LEGALAGENT_PRIVATE_CORPUS=tests/LegalAgent.PdfParser.Tests/Corpus/private \
  dotnet test LegalAgent.slnx -c Release --filter "FullyQualifiedName~PrivateCorpusTests"
```

## 2. Weryfikacja ręczna na `mbank-reg3.pdf`

```bash
dotnet run --project src/LegalAgent.PdfParser.Cli -c Release -- \
  convert tests/LegalAgent.PdfParser.Tests/Corpus/private/mbank-reg3.pdf \
  -o /tmp/reg3.md --report /tmp/reg3.report.json
```

| Sprawdzenie | Oczekiwane |
|-------------|------------|
| `grep -E "^#{1,6} " /tmp/reg3.md` | `#` tytuł, potem wyłącznie `##` z nazwami sekcji w kolejności: Organizator promocji, Uczestnik promocji, Ważne pojęcia, Korzyści promocji, Kiedy i jak możesz przystąpić do promocji?, Warunki/zasady promocji, Jak możesz złożyć wniosek w promocji?, Jak możesz złożyć reklamację dotyczącą promocji?, Dodatkowe informacje (pełna lista wg PDF) |
| `grep -c "^|" /tmp/reg3.md`, `grep -c ' \\| ' /tmp/reg3.md` | 0 i 0 (SC-013) |
| `grep -c "Definicje" /tmp/reg3.md` | 0 (wiersz nazw kolumn pominięty) |
| `grep -n "mBank.pl\|Obowiązuje od" /tmp/reg3.md` | zwykłe akapity, bez `#` |
| `grep -n "Nie możesz uczestniczyć\|Korzyści obowiązujące\|MOJE OŚWIADCZENIA" /tmp/reg3.md` | pogrubione akapity `**…**`, bez `#` |
| sekcja „Korzyści promocji” | jedna ciągła treść ze str. 4–6 (znaczniki `<!-- page: 5 -->`, `<!-- page: 6 -->` w tekście), podpunkty „o” zagnieżdżone pod „•” |
| sekcja „Ważne pojęcia” | każda definicja osobnym akapitem |
| adresy | `https://wearpay.pl/products/pierscien-platniczy-mastercard`, `…/rozwijaj-firme-z-platnosciami-od-mBanku-edycja-1-01-09-26-30-11-26.pdf` |
| `/tmp/reg3.report.json` | `tableDocuments`: 1 pozycja, strony 2–12, `headerRowText` „Definicje \| Wyjaśnienie”, `droppedHeaderRows` 8; brak `TBL001_AmbiguousGrid` |
| kompletność | słowa PDF (bez „Definicje”/„Wyjaśnienie”) ⊂ słowa wyniku — porównanie z surowym tekstem PdfPig (skrypt testu SC-010) |

## 3. Brak regresji na prywatnych regulaminach

```bash
for f in mbank-regulamin-pdp mbank-reg1 mbank-reg2; do
  dotnet run --project src/LegalAgent.PdfParser.Cli -c Release -- \
    convert tests/LegalAgent.PdfParser.Tests/Corpus/private/$f.pdf -o /tmp/$f.md
  diff tests/LegalAgent.PdfParser.Tests/Corpus/private/baseline/$f.md /tmp/$f.md
done
```

Oczekiwane: jedyna różnica w każdym pliku to linia „obowiązuje od …” zmieniona z `## …` na akapit
(FR-093). Tabela definicji w `mbank-regulamin-pdp` pozostaje tabelą GFM, schematy kroków (FR-067)
bez zmian.

## 4. Wyłączenie funkcji

```bash
PDFPARSER__Tables__DetectTableDocuments=false \
  dotnet run --project src/LegalAgent.PdfParser.Cli -c Release -- \
  convert tests/LegalAgent.PdfParser.Tests/Corpus/private/mbank-reg3.pdf -o /tmp/reg3-off.md
```

Oczekiwane: tabele jak w feature 001 (US4, scenariusz 3) — poza ogólnymi poprawkami FR-088, FR-093,
FR-094 i punktora „o”.
