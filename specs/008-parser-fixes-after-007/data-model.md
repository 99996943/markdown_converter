# Data Model: Poprawki parsera po spec 007

Zmiany addytywne w bibliotece `LegalAgent.PdfParser`; model dokumentu (`LegalDocument`, `Section`, bloki) bez zmian
kształtu. Szczegóły uzasadnień — [research.md](./research.md).

## Publiczne API

### `ListLabelKind` (`Model/ListItem.cs`) — dwie nowe wartości (US3)

| Wartość | Etykieta | Porządek | Uwagi |
|---|---|---|---|
| `LetterDot` | „a.” … „z.” (mała litera + kropka, cały token) | 1–26 | tylko w spójnym ciągu ≥ 2 na tym samym wcięciu |
| `RomanDot` | „i.” … „xxxix.” (małe rzymskie + kropka) | 1–39 | jw.; „i.”, „v.”, „x.” dwuznaczne — rozstrzyga ciąg |

Walidacja (FR-620, FR-623, FR-624): etykieta zaczyna wiersz PDF i jest osobnym słowem, po niej tekst; kandydat
przyjęty tylko, gdy najbliższy kandydat na tym samym wcięciu (± 1,5 pt) przed nim albo po nim jest jego poprzednikiem
lub następnikiem w ciągu, a między nimi są tylko wiersze wcięte dalej niż etykieta. „i.” po „h.” — `LetterDot`; „i.”
z następnikiem „ii.” — `RomanDot`. Samotna „a.”, „r.”, ciąg niespójny („a., c.”) — tekst. Wielkie litery — bez zmian
(poza zakresem). Zagnieżdżenie z wcięcia (`Rank` = null). Renderowanie: `- a\.`, `- ii\.` (istniejące
`EscapeListLabel`).

Uszczegółowienie po implementacji (T026–T028): dopasowanie „i.”, „v.”, „x.” ma rodzaj `LetterDot` i drugi odczyt
`RomanDot` w `ListLabelMatch.Alternative`; bramka bierze odczyt zgodny z poprzednikiem, a bez niego — z następnikiem.
Wiersz przerywający szukanie sąsiada: wiersz innej roli (nagłówek, tabela), jednostka redakcyjna albo wiersz nie dalej
niż kolumna etykiety; odrzucony kandydat traci etykietę (zostaje zwykłym wierszem). Etykieta w nawiasie („[a.”) nie
jest kandydatem. Dodatkowo (odstępstwo od planu, wymuszone przez s. 38 D-A): w `TableDetectionStage.TrimTrailingLines`
końcowy wiersz wewnątrz siatki, który otwiera element listy (etykieta złączona z tekstem), w wierszu siatki
obejmującym całą ramkę (tylko zewnętrzne linie pionowe) kończy tabelę — bez tej reguły po złączeniu „i.” z tekstem
tabela zastępcza zmieniała się w tabelę GFM z całym fragmentem „1.”, „2.”, „a)”, „i.”, „ii.” w jednej komórce.

Konsumenci wartości enum: `ListDetectionStage` (bramka, `KeepLabelledBoldTextInLists`), `TableDetectionStage`
(`IsHangingLabelBefore`), `TableDocumentStage.HasListOrParagraphs` (nowe rodzaje **wyłączone**), `GlossaryDetection`
(bez zmian — filtruje rodzaje), `FaqGrounding.ListLabel` w `LegalAgent.Faq` (sprawdzić, czy pomija etykiety „a.”,
„ii.” przy porównaniu cytatów — jeśli nie, dopisać jak T041a w spec 007).

### `ArtifactOptions.PageNumberBandRatio` (`Options/PdfParserOptions.cs`) — nowa opcja (US1)

| Pole | Typ | Domyślnie | Walidacja | Znaczenie |
|---|---|---|---|---|
| `PageNumberBandRatio` | `double` | 0,15 | jak `MarginZoneRatio` (0 < x < 0,5) i ≥ `MarginZoneRatio` | wysokość dolnego i górnego pasa, w którym szuka się samodzielnych numerów „N/M” |

Nadpisywana w CLI przez `PDFPARSER__Artifacts__PageNumberBandRatio`.

## Wewnętrzne pojęcia (bez zmian API)

### Ciąg numerów stron „N/M” (US1, `ArtifactRemovalStage`)

- Kandydat: wiersz, którego cały tekst to `^\d{1,3}\s*/\s*\d{1,3}$`, w pasie `PageNumberBandRatio`, najniższy (dolny
  pas) albo najwyższy (górny pas) wiersz strony, bez innego wiersza na tej wysokości.
- Ciąg (usuwany w całości albo wcale): kandydaci z ≥ 2 stron, ta sama względna wysokość (± `PositionTolerance`), to
  samo M = liczba stron dokumentu, to samo przesunięcie N − numer strony.
- Raport: wpis `ArtifactKind.PageNumber` jak dla dotychczasowych numerów stron.

### Wiersz kontynuacji (US2, pomocnik w `Text/` lub `Layout/`)

Wiersz pasujący do wzorca jednostki (`LegalUnitPatterns`) jest kontynuacją, gdy wszystkie warunki:

| Warunek | Wartość |
|---|---|
| wiersz powyżej (ta sama strona; wiersz na górze strony nigdy nie jest kontynuacją — FR-610) | rola Unknown, ListItem albo ListContinuation |
| odstęp | ≤ 1,2 × interlinia |
| rozmiar / `AllBold` | ten sam dominujący rozmiar (± 0,5 pt) i to samo `AllBold` |
| pogrubienie słowa | pierwsze słowo oznaczenia = pogrubienie ostatniego słowa poprzedniego wiersza |
| koniec poprzedniego wiersza | nie `.`, `:`, `;`, `!`, `?` (także przed zamykającym nawiasem lub cudzysłowem: „naboru.]”) |
| pełny wiersz powyżej | prawa krawędź wiersza powyżej ≥ prawa krawędź wiersza − 2 pt (T017: zawija się tylko pełny wiersz) |
| lewa krawędź | ≈ lewa krawędź poprzedniego wiersza albo początek jego tekstu za etykietą (± 2 pt) |
| wyśrodkowanie | wiersz nie jest wyśrodkowany |

Skutek: `ListDetectionStage` nie ustawia `LegalUnit` (wiersz jest kontynuacją elementu), `HeadingDetectionStage` nie
tworzy nagłówka. Dotychczasowa reguła FR-162 (`ContinuesSentence`, mała litera) zostaje.

### Ramka schematu kroków (US4, `StepSequenceStage`, adnotacje `step.*` bez zmian)

| Część | Rozpoznanie |
|---|---|
| Pole | wypełnienie niebiałe, szerokość < 50% strony, brak linii w pasie pola; tekst w polu ≥ 80% pogrubionych znaków |
| Fraza (StepTitle) | wiersze, których środek leży w polu; złączone w jeden pogrubiony akapit |
| Objaśnienie | wiersze po prawej od pola w jego pasie **oraz przelew**: niczyje wiersze po prawej poniżej pola do następnego pola, obcego tekstu po lewej albo końca strony, przy odstępie ≤ 1,5 × wysokość wiersza |
| Łącznik | wiersz (lub słowa na lewo od prawej krawędzi pola) bez liter i cyfr (obszar prywatny Unicode, czcionki symboli) w zakresie x pól, między polami → `Artifact`; nie przerywa schematu |
| Wiersz nagłówków | tylko wiersz w całości pogrubiony nad pierwszym polem; niepogrubiony wiersz nigdy nie jest nagłówkiem |
| Schemat | ≥ 1 pole (pojedyncze pole tylko przy spełnionej osłonie pola) |

## Miary (pomocnik testowy `Fixtures/LayoutMetrics.cs`)

Dotychczasowe (spec 007): TBL001, wiersze „ \| ”, etykiety poza listą, „§ N” jako tekst, spis treści, „N/M”. Nowe:

| Miara | Definicja |
|---|---|
| `PageNumberParagraphs` | akapity, których cały tekst to „N/M” |
| `UnitHeadingsFromContinuation` | nagłówki jednostek, których wiersz źródłowy spełnia regułę kontynuacji (pomiar przez porównanie z punktem odniesienia: lista nagłówków przed/po) |
| `DotEnumLabelsOutsideList` | tokeny „a.”…/„i.”… należące do ciągu, które nie zaczynają elementu listy (w tekście elementu, w akapicie, w komórce) |
| `FramesAsTable` / `FramesWoven` | ramki schematu zapisane w tabeli GFM / wplecione w element listy (z listy fraz ramek dokumentu w nieśledzonym pliku prywatnym, jak `layout-labels.txt`) |
| Zbiory słów | słowa (bez interpunkcji i znaczników Markdown) goldenu przed i po; różnica raportowana jako utracone / nowe |
