# Data Model: Poprawki parsera po przeglądzie liczb (spec 009)

Model dokumentu biblioteki (`LegalDocument`, `PdfConversionResult`, `ConversionReport`) i opcje parsera **bez
zmian**. Nowe są wyłącznie pojęcia wewnętrzne etapu składania wierszy i typy pomocnika testów.

## Biblioteka (wewnętrzne)

### Ciąg tekstu własnego (`LineAssemblyStage`, prywatne)

Zbiór indeksów glifów strony wyznaczany przed `GroupIntoLines`.

| Pole / reguła | Opis |
|---|---|
| Członkowie | glify niebędące białymi znakami, jednego rozmiaru (± 0,1 pt), na wspólnej linii bazowej (`BaselineToleranceRatio` × rozmiar) |
| Spójność | kolejne glify (po x) oddalone o ≤ `NearGlyphEm` × własny rozmiar |
| Próg | ciąg ma ≥ 4 glify (stała prywatna, np. `OwnLineMinGlyphs = 4`) |
| Skutek | w `LineBuilder.Accepts`: glif ciągu, mniejszy niż `MarkerSizeRatio` × zarodek, w odległości > `NearGlyphEm` × mniejszy rozmiar od glifów wiersza → `false` (przed obu ścieżkami: tolerancją linii bazowej i pionowym nakładaniem) |

Niezmienniki: glify spoza ciągów (indeksy, odnośniki 1–3 glifów) i glify przy słowie (odległość ≤ próg) zachowują
się jak dotąd; wynik deterministyczny (sortowanie po linii bazowej, x, indeksie).

### `LegalNumberTail` (`TextNormalizationStage`)

`(?:(?<!\p{L})[Aa]rt\.|§)\s*\d+[a-z]*[⁰¹²³⁴-⁹]*$` — reszta reguły (`IsRaisedSmallDigit`: ≤ 0,8 rozmiaru, podniesienie
≥ 0,15 rozmiaru) bez zmian.

## Testy

### `NumberDiff` (`Fixtures/NumberCheck.cs`)

| Pole | Typ | Opis |
|---|---|---|
| `Excess` | `IReadOnlyDictionary<string,int>` | liczby z krotnością o ile częściej w Markdownie niż w PDF (zmienione, sklejone) |
| `Missing` | `IReadOnlyDictionary<string,int>` | liczby o ile częściej w PDF niż w Markdownie (poza wierszami artefaktów) |
| `PdfCount`, `MarkdownCount` | `int` | liczba tokenów liczbowych po obu stronach (kontrola sensowności) |
| `Skipped` | `string?` | powód pominięcia (np. brak warstwy tekstowej) albo `null` |

Klucz liczby: dokładny tekst dopasowania `[0-9]+(?:[.,][0-9]+)*` (bez normalizacji separatorów — „150.000” i
„150 000” to różne zapisy).

### Liczby PDF (`NumberCheck.FromPdf(path, report)`)

- litery PdfPig strony w kolejności strumienia; podział na tokeny: biały znak, przerwa > 0,15 em, zmiana linii
  bazowej > 0,2 × rozmiar, przejście do/od cyfry indeksu;
- cyfra indeksu: ≤ 0,8 rozmiaru poprzedniej pełnej litery i podniesiona ≥ 0,15 jej rozmiaru — nie jest liczbą;
- wiersz surowy (litery jednej linii bazowej strony) z `LineFingerprint` równym `ArtifactSummary.Pattern`, na stronie
  z `ArtifactSummary.Pages` — pomijany.

### Liczby Markdown (`NumberCheck.FromMarkdown(markdown)`)

Usuwane przed dopasowaniem: `<!-- … -->`, `[^n]:` na początku wiersza, `[^n]`, `\` ucieczki. Znaki `⁰`–`⁹` nie są
cyframi.

### Lista regulaminów (`number-check.txt`, nieśledzony)

Patrz [contracts/number-check.md](./contracts/number-check.md). Encja: nazwa dokumentu (bez `.pdf`) + opcjonalne
dopuszczone liczby nadmiarowe (wyjątek z uzasadnieniem, używany tylko przy wycofaniu US3). Logikę bramki
(parsowanie listy, odejmowanie dopuszczonych, komunikaty) zawiera czysta funkcja `RegulationGate.Evaluate(listText,
diffs)` w `Fixtures/NumberCheck.cs`, testowana offline.

### `LayoutMeasures` (rozszerzenie)

Nowe pola `NumbersExcess`, `NumbersMissing` (sumy krotności) wypełniane, gdy `Measure` dostaje ścieżkę PDF; w
raporcie kolumny „Liczby +” i „Liczby −” oraz sekcja z wartościami dla dokumentów z niezerową miarą.
