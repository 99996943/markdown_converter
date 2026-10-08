# Contract: format wyjściowy Markdown

Docelowo: **CommonMark 0.31** + rozszerzenia GFM: tabele, przypisy (`[^n]`). Kodowanie UTF-8,
separator linii LF, plik kończy się jednym `\n`. Komentarze HTML są jedynym rodzajem HTML w wyniku.

## Kolejność renderingu

```text
# <Title>                         (jeśli wykryty)
<Preamble blocks>
<PreambleFootnotes definitions>
## / ### … <Section.HeadingText>  (rekurencyjnie: nagłówek → Blocks → Children → Footnotes sekcji)
```

Bloki rozdzielone dokładnie jedną pustą linią. Definicje przypisów sekcji renderowane po jej
treści własnej i podsekcjach — dla Art./§ (liście) oznacza to koniec artykułu (Clarifications Q5).

## Elementy

| Element modelu | Rendering | Przykład |
|----------------|-----------|----------|
| `Section` | `#` × `Level` + spacja + `HeadingText` | `### Art. 5.` |
| `ParagraphBlock` | inliny w jednej linii (bez twardych łamań) | `Bank pobiera opłatę …` |
| `TextRun` Bold / Italic / oba | `**t**` / `*t*` / `***t***` (białe znaki na brzegach wyjęte poza znaczniki) | `**zmiana**` |
| `FootnoteRef` | `[^n]` | `ustawa[^1]` |
| `Footnote` | `[^n]: treść` | `[^1]: Niniejsza ustawa wdraża …` |
| `PageBreak` (opcja `PageMarkers`) | `<!-- page: N -->`; na początku bloku — osobna linia przed blokiem; wewnątrz akapitu/pozycji — wstawiony między słowami ze spacjami | `… zawarcia <!-- page: 5 --> umowy …` |
| `ListItem` | `- ` + oznaczenie z ucieczką + spacja + treść; poziom zagnieżdżenia = 2 spacje wcięcia | `- 1\) definicja` / `  - a\) lit.` |
| `ListItem` z `Bullet` | `- ` + treść (znak punktora pominięty) | `- karta debetowa` |
| `ListItem` z `Dash` (tiret) | `- – treść`; dywiz `-` jako `\-` (by nie powstała lista zagnieżdżona) | `- – w przypadku …` |
| `ListItem` — dzieci | zagnieżdżona lista: kolejne linie, wcięcie +2, bez pustej linii; akapit „części wspólnej”: pusta linia, treść z wcięciem, pusta linia przed kolejną pozycją | `  część wspólna` |
| `ListItem` rozpoczęty na nowej stronie | `<!-- page: N -->` w osobnej linii (z wcięciem pozycji) przed pozycją, o ile N ≠ bieżąca strona | `  <!-- page: 5 -->` |
| `TableBlock` | tabela GFM; nagłówek = `Header` lub pierwszy wiersz; separator `| --- |`; `|` w treści → `\|`; znaczniki stron w tabeli pominięte, znacznik kolejnej strony po tabeli | `| Usługa | Opłata |` |
| `TableBlock` (fallback) | każdy wiersz wizualny jako osobny akapit (oddzielony pustą linią), komórki połączone ` \| ` (z ucieczką, by nie powstała tabela GFM) | `Prowadzenie rachunku \| 0 zł \| miesięcznie` |
| Schemat kroków (FR-067) | dla każdego kroku pogrubiony akapit z oryginalną nazwą kroku (`ParagraphBlock`, bez dopisanego tekstu), po nim wyjaśnienie jako zwykłe akapity i listy; wiersz nazw kolumn pominięty; bez tabeli i bez nagłówka `#` | `**Składasz wniosek**` |
| `SkippedPageBlock` | `<!-- page N skipped: no-text-layer -->` / `<!-- page N skipped: read-error -->` | |

## Ucieczka znaków w tekście

W `TextRun` escapowane są: `\ * _ [ ] < > `` ` `` oraz `|` wewnątrz tabel. Na początku linii
akapitu dodatkowo: `#`, `+`, `-`, `>`, `=`, oraz wzorce `\d+[.)]` (np. `2024\. r.`) — aby akapit
nie stał się nagłówkiem/listą. Oznaczenia list: `1)` → `1\)`, `2.` → `2\.`, `a)` → `a\)`.

## Niezmienniki (testowane)

1. Brak dwóch kolejnych pustych linii.
2. Żaden wiersz nie kończy się spacją (twarde łamania linii nie są używane).
3. Każdy `[^n]` ma dokładnie jedną definicję `[^n]:`; numeracja ciągła od 1.
4. Poziom nagłówka rośnie o co najwyżej 1 względem rodzica.
5. Po usunięciu komentarzy `<!-- … -->` wynik jest identyczny z renderingiem `PageMarkers = false`
   (modulo pojedyncze spacje wokół znacznika).
6. Numery w znacznikach `<!-- page: N -->` rosną: blok z wcześniejszej strony umieszczony po treści strony
   późniejszej (np. adnotacja boczna, FR-034) nie dostaje znacznika.
