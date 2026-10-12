# Research: Poprawki tabel i przypisów parsera (spec 010)

Źródła: sonda PdfPig 0.1.16 na prywatnych PDF-ach (zrzut liter i linii w katalogu tymczasowym sesji, poza git),
przegląd kodu `TableDetectionStage`, `FootnoteDetectionStage`, `MarkdownRenderer`, `LegalAgent.Chunking`,
`NumberCheck`. Strony 1-based, współrzędne PDF (y w górę), strony 595,3×842. Wszystkie linie w tych dokumentach to
cienkie wypełnione prostokąty 0,48 pt, osobny odcinek na każdą komórkę / kolumnę / wiersz.

## R1 — Kolumny tabel z liniami wyznaczane z linii pionowych (T1, T2, T3, T5, T6)

**Ustalenie.** Wbrew PRZYPADKI.md wszystkie te tabele są **w pełni z liniami**:

| Przypadek | Linie pionowe (x) | Obserwacja |
|---|---|---|
| T1 ind-4 s. 42 | 45,1 / 187,0 / 280,5 / 374,1 / 464,9 / 555,6 | wiersze 23,2–112,1 pt; wartości wyśrodkowane w pionie (linia bazowa = średnia linii 1. kolumny), odstęp międzywierszowy/odstęp w komórce = 1,29 |
| T2 firm-1 s. 16–17 | z liniami (PRZYPADKI: „bez ramek” — błąd) | jak T1, nagłówek 3-liniowy |
| T3 downloads-2 s. 20 | 45,7 / 185,4 / 278,9 / 372,3 / 463,1 / 555,5 | linie nagłówka „serwis” 212,0–252,3, „transakcyjny” 192,1–272,2, „nasza strona” 196,8–267,5 — wszystkie środki x ≈ 232; dane 221,1–243,1 |
| T3 firm-3 s. 8 | 59,2 / 215,2 / 328,6 / 442,1 / 555,5 | wiersz grupujący „Gdzie można zrobić?” nad kolumnami 2–4: linia 266,3 ma odcinek 215,4–555,2 (jeden przez 3 kolumny), linia 255,9 tylko nad kolumnami 2–4 → kolumna 1 obejmuje oba wiersze nagłówka |
| T5 ind-2 s. 6–7 | 43,7 / 325,6 / 438,4 / 551,3 | kolumny 2–3 wyśrodkowane: lewe krawędzie w jednej kolumnie różnią się o 12,6 i 24,4 pt |
| T6 ind-4 s. 12→13 | 59,3 / 201,1 / 378,3 / 555,5 | na s. 13 nagłówek powtórzony z identycznymi x słów i odstępami od górnej linii |

**Przyczyna.** `TableDetectionStage` (ok. 483–493) wyznacza kolumny przez `ColumnClustering.ClusterLefts` z lewych
krawędzi komórek wszystkich wierszy (nagłówek i dane razem, `MinBandSupport` = 2), a linie pionowe tylko „dociągają”
pasma (`Bands`). Wyśrodkowane linie nagłówka i wartości mają różne lewe krawędzie, więc powstają fałszywe pasma
(puste kolumny, „TAK” pod złym nagłówkiem), a dwie komórki w jednym paśmie wywołują format zapasowy TBL001 (527–528).

**Decyzja.** Nowy pomocnik `RuledTableGrid` (plik `Stages/RuledTableGrid.cs`) dla tabel, w których `Grid` znajduje
siatkę (linie poziome i pionowe przecinające pasmo):
- **kolumny** = x linii pionowych występujących w co najmniej dwóch wierszach siatki (łączone z tolerancją 3 pt);
  pasma kolumn między kolejnymi liniami — zamiast klastrowania lewych krawędzi;
- **przypisanie tekstu**: słowo/odcinek linii trafia do pasma według **środka** x (nie lewej krawędzi); linie nagłówka
  w jednym paśmie składają się w jedną komórkę (FR-720, FR-722);
- **komórka przez kilka kolumn**: odcinek linii poziomej nad komórką, który ciągnie się przez kilka pasm bez linii
  pionowej w tym wierszu, daje `ColumnSpan` (wiersz grupujący firm-3).
Tabele bez siatki (bez linii pionowych) — dotychczasowa ścieżka bez zmian. Niezbadane jeszcze tabele T3/T1
(downloads-3, 4, 5, firm-5 s. 22–23) sprawdza sonda T013a przed wpięciem siatki. **Wynik T013a**: wszystkie w pełni z liniami
(downloads-3/4/5: 5 kolumn, wartości wyśrodkowane, nagłówki 5–7 linii powtórzone na kolejnych stronach; firm-5 s. 22:
4 kolumny z wierszem grupującym nad kolumnami 2–4 jak firm-3). Wariant bez linii (US4 scen. 2, małe
tabele bez linii z FR-741) jest P3: po ścieżce z liniami próba „kolumny z wierszy danych, nagłówek wg środka x” w
klastrowaniu; jeśli pogorszy taryfy korpusu syntetycznego lub słowniki — „Znane ograniczenia”.

**Alternatywy odrzucone.** Strojenie `ColumnTolerance`/`MinBandSupport` — nie usuwa przyczyny (wyśrodkowanie), a
grozi taryfom bez siatki. Białe prostokąty pod komórkami (sygnał scalenia w firm-5) — `PageExtraction` je pomija, a
odcinki linii wystarczają.

## R2 — Granice wierszy osobno dla każdej kolumny; scalenie pionowe (T11, T1)

**Ustalenie (firm-5 s. 13).** Linie pionowe x = 59,3 / 201,1 / 555,6 (przerywane przy każdej linii kolumny 2, także
wewnątrz scalonej komórki — przerwy linii pionowych niczego nie mówią). Linie poziome: odcinki dla kolumny 1
(59,5–200,8) i kolumny 2 (201,3–555,3); oba odcinki na y 654,2 / 640,7 / 513,1 / 499,9 / 397,9 / 377,3 / 356,8,
tylko kolumna 2 na 627,3 / 596,9 / 489,3 / 458,8 / 428,4. Etykieta „przelew wychodzący SWIFT” — jedna linia 573,8 na
środku grupy (640,7→513,1), wpada dziś do wiersza ⁵; „(wewnętrzny)” — 2 linie 450,8 i 440,8 dzielą linie bazowe z
wierszem ⁸. Tekst 8 pt, interlinia w komórce 10,0.

**Przyczyna.** `HorizontalRulings` (679–722) łączy współliniowe odcinki i przyjmuje y, gdy najdłuższy ciąg ma
≥ `RulingSpanRatio` (0,4) szerokości obszaru; nie pamięta, które kolumny linia przecina. W T1 (ind-4) wiersze są
wyznaczane dobrze tylko wtedy, gdy wszystkie linie obejmują całą szerokość, ale wartości wyśrodkowane w pionie
tworzą z liniami 1. kolumny wiersze „mieszane” (ta sama linia bazowa) albo z pustą 1. kolumną.

**Decyzja.** `RuledTableGrid` buduje siatkę komórek:
- dla każdego pasma kolumny zbiór y odcinków linii poziomych pokrywających ≥ 80% szerokości pasma;
- granice wierszy tabeli = suma tych zbiorów; komórka pasma b obejmuje kolejne wiersze, między którymi w paśmie b nie
  ma linii → scalenie pionowe (`RowSpan`), jeśli w innym paśmie linia jest;
- tekst przypisywany do komórki **po geometrii** (linia bazowa między liniami poziomymi komórki, środek x w paśmie),
  słowo po słowie, a nie całymi liniami układu — linia układu z wartościami wyśrodkowanymi w pionie i środkową linią
  1. kolumny jest rozdzielana na komórki; tekst komórki w kolejności linii bazowych.
Wiersze tabel z siatką nie zależą już od odstępów międzywierszowych (rozwiązuje T1 i T4 z liniami). Tabele bez siatki
— dotychczasowe `HorizontalRulings` i grupowanie wierszy.

**Alternatywy odrzucone.** Obniżenie `RulingSpanRatio` — linie kolumny 2 (60% szerokości) przeszłyby, ale scalenie
nadal by ginęło; heurystyka „pusta 1. kolumna = kontynuacja” — psuje tabele z celowo pustymi komórkami.

## R3 — Model scalenia pionowego i chunker

**Ustalenie.** `UnitSplitter.Blocks` (Chunking, 140–151) składa część tabeli z **podzbioru** obiektów `TableRow`
(`first.Table with { Rows = rows }`, nagłówek kopiowany). Komórka scalona zapisana raz w pierwszym wierszu i
powtarzana przez renderer „w przód” zginęłaby w częściach zaczynających się w środku grupy (US1 scen. 4) i wychodziła
poza listę wierszy na końcu części. `TableRow` ma niezmiennik „suma `ColumnSpan` = `ColumnCount`”
(specs/001 data-model).

**Decyzja.** `TableCell` dostaje dwa pola z wartościami domyślnymi: `RowSpan` (≥ 1, w pierwszym objętym wierszu) i
`ContinuesAbove` (bool; w kolejnych objętych wierszach komórka z **tymi samymi** inline'ami). Każdy wiersz zachowuje
pełny zestaw kolumn, więc niezmiennik, renderer (powtarza tekst, bo każda komórka ma inline'y), chunker
(`Blocks`, `PageTracker`, `FootnoteSelector`) i numeracja przypisów (`DocumentBuildStage.InlinesOf` — pierwsze
odwołanie wyznacza numer, kolejne odwołania do tego samego przypisu dają ten sam `[^n]`) działają bez zmian logiki.
Konsument modelu, który chce scalenia, czyta `RowSpan`/`ContinuesAbove`. Test chunkera: część zaczynająca się w
środku grupy ma etykietę (US1 scen. 4) — test regresji, bez zmian kodu chunkera.

**Alternatywy odrzucone.** (a) Renderer powtarzający „w przód” + przycinanie spanów w `UnitSplitter.Blocks` — logika
w dwóch projektach, łatwa do rozjechania; (b) samo powtórzenie tekstu bez pola w modelu — sprzeczne z decyzją
właściciela („model zna scalenie”).

## R4 — Wiersz grupujący nad nagłówkami (T3 firm-3, decyzja właściciela)

**Decyzja.** `TableBlock` dostaje opcjonalne `HeaderGroup` (`TableRow?`): komórki wiersza grupującego z `ColumnSpan`
według siatki (R1). Renderer GFM pisze w każdej kolumnie nagłówka: tekst komórki grupującej obejmującej tę kolumnę +
spacja + tekst nagłówka kolumny („Gdzie można zrobić? BOK”); kolumna bez komórki grupującej (np. „Co można zrobić?”,
która w PDF obejmuje oba wiersze nagłówka jako scalenie pionowe) — sam nagłówek. Chunker kopiuje `TableBlock`
przez `with`, więc `HeaderGroup` jest w każdej części. Wiersz grupujący rozpoznawany tylko w siatce: wiersz nad
nagłówkiem, którego komórki mają `ColumnSpan` > 1 lub obejmują część kolumn, a kolumny bez komórki są scalone
pionowo z nagłówkiem.

**Alternatywy odrzucone.** Wpisanie tekstu grupy w inline'y komórek nagłówka — kontrola liczb i model nie
odróżniłyby powtórzenia od tekstu.

## R5 — Przypisy: odnośnik przed słowem, w komórkach tabel, definicje gołą cyfrą (T12)

**Ustalenia z kodu.**
- `FindMarkers` (FDS 94–126) idzie po glifach **od końca** słowa (rozmiar ≤ 0,85 linii, podniesienie ≥ 0,15 em) —
  odnośnik na początku słowa („³kurs”) nigdy nie jest znaleziony.
- `TableDetectionStage.RowDraft.Add` (1053–1056) kopiuje tylko tekst i styl słowa — **`FootnoteId` ginie w komórkach
  tabel**; rozpoznane odnośniki w tabelach wychodzą jako zwykły tekst, a definicja jako nieprzypisana na końcu sekcji.
- `FootnoteArea` (144–171), ścieżka z kreską (kreska w dolnej części strony, `Y1 ≥ 0,6·Height` w układzie
  parsera, długość ≤ 0,6 szerokości): wszystkie linie pod kreską muszą mieć rozmiar ≤ `MaxSizeRatio` (0,9) tekstu.
- `DefinitionLabel` `^(\d{1,3}\)|\*{1,3}\)?)\s+…` — goła cyfra nie jest etykietą; linia bez etykiety na początku
  obszaru tworzy szkic z pustą etykietą, do którego doklejane są kolejne linie → „[^1]: 1 obowiązuje… 5 O
  wprowadzeniu… 8 Dotyczy…” (firm-1, ind-2, ind-4, corp-5 s. 25).
- Numeracja wyjścia jest kolejna (`DocumentBuildStage.NumberFootnotes`), etykieta z PDF w `Footnote.OriginalLabel`.

**Ustalenia z PDF.** firm-5 s. 13: odnośniki 5 pt podniesione o 3,0 nad tekstem 8 pt, „³” na początku tekstu komórki
(x 206,4–209,7, „kurs” od 209,7 bez odstępu); kreska y 143,9, x 39,7–183,7 (zaczyna się na lewo od marginesu
53,9); definicje 8 pt (= tekst główny), interlinia 9,7–9,8; etykiety ³–⁷ 5 pt podniesione, 8 i 9 — zwykłe 8 pt na
linii bazowej. firm-1 s. 16: „TAK/TAK⁵” (5 pt, +3,6), definicje na tej samej stronie pod kreską 97,7, tekst 6 pt
(dotychczasowy przypadek mniejszej czcionki — psuje go tylko etykieta bez nawiasu). ind-4 s. 42: odnośnik ⁷ 7 pt,
definicja 7 pt z etykietą 4,6 pt.

**Decyzja.**
1. **Etykiety definicji (geometria)**: linia obszaru przypisów zaczynająca się od małych podniesionych cyfr (te same
   progi co odnośnik) jest etykietą definicji, z odstępem lub bez; etykieta gołą cyfrą `^\d{1,3}\s` dozwolona tylko
   w obszarze pod kreską i tylko dla numerów, do których są odnośniki na stronie (FR-713).
2. **Obszar pod kreską przy rozmiarze tekstu głównego** (FR-712): ścieżka z kreską przyjmuje linie większe niż
   `MaxSizeRatio`, jeśli pierwsza linia pod kreską i każda linia zaczynająca definicję mają etykietę (pkt 1) z
   odnośnikiem na stronie; linie bez etykiety są ciągiem dalszym poprzedniej definicji.
3. **Kolejność**: obszar kandydacki pod kreską wyznaczany przed szukaniem odnośników, a podniesione etykiety na
   początku jego linii nie są odnośnikami.
4. **Odnośnik na początku słowa** (FR-710): `FindMarkers` sprawdza też prefiks słowa (glify od początku, te same progi,
   po prefiksie musi być litera lub cyfra pełnego rozmiaru), z tą samą regułą powiązania co odnośnik na końcu
   słowa (definicja na tej samej albo dalszej stronie — `LinkMarkers`); `LinkMarkers` dzieli słowo w kolejności
   odnośnik → reszta; w `InlineAccumulator` odnośnik przed słowem bez spacji: `[^n]kurs`.
5. **Odnośniki w komórkach** (FR-711): `RowDraft`/`Token` przenoszą `FootnoteId` i emitują `FootnoteRef`;
   `RuledTableGrid` (R2) przypisuje słowo-odnośnik do komórki słowa, do którego przylega.
6. **Ochrona** (FR-715): indeksy po „art.”/„§” zamieniane wcześniej w `TextNormalization` (znak `¹` nie pasuje do
   `MarkerLabel`) — bez zmian; odnośnik wymaga definicji (ta sama albo dalsza strona), a etykieta gołą cyfrą pod
   kreską — odnośnika na tej samej stronie; zwykłe liczby mają pełny rozmiar.

**Ryzyko — goldeny chronione.** Pkt 5 zmienia każdą tabelę z rozpoznanym dziś odnośnikiem (np. akty z przypisami
w tabelach, taryfy `Corpus/banking`): z „opłaty 1)” na „opłaty[^n]” i przeniesienie definicji. To zmiana goldenów
spoza wąskiej zgody → stop i pytanie (FR-755), z diffem przygotowanym przez wykonawcę. Pierwszy krok implementacji:
policzyć te miejsca (test sondy na `Corpus/acts`, `Corpus/banking`, korpusie syntetycznym).

**Poza zakresem.** Odnośniki z przecinkiem („BLIK⁹,¹⁰”) i „art. 166¹¹²)” (spec 009) — „Znane ograniczenia”.

## R6 — Powtórzony nagłówek i komórka urwana na końcu strony (T6, T5)

**Ustalenie.** ind-4 s. 13: nagłówek powtórzony z identycznymi tekstami i x słów; poniżej wiersz z pustymi
kolumnami 1–2 i 11 liniami listy w kolumnie 3 (ciąg „kurs crossowy, czyli:” z s. 12, zamkniętej linią na 75,0).
ind-2 s. 6: nagłówek + 1 wiersz na dole strony, s. 7: nagłówek powtórzony + 4 wiersze. Dziś nagłówek s. 13 nie
wchodzi do tabeli (kolumny z R1 → tabela nierozpoznana / inny podział), więc `HeadingDetection` robi z niego
nagłówek typograficzny (pierwsza linia strony, cała pogrubiona, ≤ 120 znaków).

**Decyzja.**
- Po R1 tabela na stronie następnej ma te same pasma; `MergeAcrossPages` łączy jak dotąd (`SameColumns`), a
  `Append` pomija pierwszy wiersz, gdy jego teksty komórek po normalizacji białych znaków są równe nagłówkowi (dziś
  porównanie `Ordinal` całego klucza); jeśli tabela ma `HeaderGroup`, porównywany i pomijany jest też powtórzony
  wiersz grupujący nad nagłówkiem (FR-742). Warunek łączenia „2+ wiersze” (`StartsAtPageEnd`/`ContinuesFromPreviousPage`)
  liczy wiersze obu części (FR-741, ind-2 s. 6 ma 1 wiersz danych).
- Kontynuacja komórki (FR-743): pierwszy wiersz danych części z następnej strony, który ma tekst w **dokładnie
  jednej** kolumnie (pozostałe puste), a ostatni wiersz poprzedniej części ma w tej kolumnie tekst, jest doklejany
  do tej komórki (nowy akapit/punkt listy w komórce). Wiersz z tekstem w dwóch lub więcej kolumnach — np. pusta
  1. kolumna (scalenie, które w PDF przechodzi przez stronę) i wartości w pozostałych — jest zwykłym wierszem
  (przypadek brzegowy spec).
- Tabela o tym samym nagłówku po akapicie (nie na początku strony) — nie jest łączona (`StartsPage`/`EndsPage` jak
  dotąd).

## R7 — Kontrola liczb i porównanie słów z powtórzeniami (FR-758)

**Ustalenie.** `NumberCheck.Compare(markdown, pdf, report)` porównuje wielozbiory; nie zna modelu. Powtórzony tekst
komórki scalonej lub grupy z liczbą dałby liczbę nadmiarową (bramka regulaminów padłaby).

**Decyzja.** `Compare` dostaje opcjonalny `LegalDocument`; liczby z komórek `ContinuesAbove` i z komórek
`HeaderGroup` × (liczba objętych kolumn − 1) są odejmowane od strony Markdownu przed porównaniem. Wywołania w
`PrivateCorpusTests` przekazują `Result.Document`. Test na syntetycznym PDF z liczbą w scalonej komórce. Porównanie
zbiorów słów przed/po (skrypt handoffu) odejmuje tak samo słowa z powtórzeń — opis w quickstart.

## R8 — Wersje, kontrakt, kolejność, ryzyka

- Publiczny model: `TableCell.RowSpan`, `TableCell.ContinuesAbove`, `TableBlock.HeaderGroup` (parametry z wartościami
  domyślnymi na końcu rekordów — zgodność źródłowa) → **1.3.0** (MINOR) w `Directory.Build.props`, `parserVersion` i
  `generatorVersion` w `corpus/manifest.json`; kontrakt Markdown **1.4.0** (contracts/markdown-output.md).
- Bez nowych opcji publicznych; progi (80% pokrycia pasma, 3 pt łączenia linii pionowych, ≥ 2 wiersze z linią
  pionową) jako stałe w `RuledTableGrid` z komentarzem — jak stałe w `FootnoteDetectionStage`.
- **Kolejność** (najmniejsze ryzyko najpierw, każdy krok z pełnym zestawem FR-753):
  1. Sonda zasięgu (test pomocniczy, bez zmian zachowania): liczba tabel z siatką i z odnośnikami w goldenach
     chronionych — ocena ryzyka R1/R5.
  2. US2 przypisy (R5 pkt 1–4, potem pkt 5 osobno — zmienia goldeny chronione).
  3. US3 + US4 kolumny i wiersze z siatki (R1, R2 bez scaleń) — największa zmiana; goldeny ind-*, firm-*.
  4. US1 scalenie pionowe + model (R2, R3) i wiersz grupujący (R4); kontrola liczb (R7) przed goldenami.
  5. US5 (R6), warianty bez linii (P3).
  6. Wydanie: wersje, refresh/verify, README, CLAUDE.md, handoff, publish/.
- **Ryzyka**: (a) `RuledTableGrid` zmienia tabele z siatką w `Corpus/acts` (załączniki, formularze), `Corpus/banking`
  i w korpusie syntetycznym (`tabela-dokument`, taryfy z liniami) — tabele-dokumenty (spec 002) mają własną ścieżkę
  (`TableDocumentIndex`) i nie wchodzą do `FindPageTables`; dla pozostałych stop i pytanie według FR-755 z diffem;
  jeśli różnice są szerokie i nie są poprawą — ograniczenie siatki do tabel, w których klastrowanie daje
  niejednoznaczność (TBL001) albo pasma inne niż linie pionowe; (b) wydajność: siatka liniowa po posortowaniu
  odcinków strony, bez regresji SC-007.
