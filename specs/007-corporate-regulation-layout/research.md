# Research: Regulaminy z etykietami w wysuniętej kolumnie i paragrafami „§ N”

Badania na czterech prawdziwych dokumentach D-A…D-D (prywatne, poza git) z pomiarem geometrii (PdfPig, sondy etapów
w scratchpadzie). Wszystkie cztery mają ten sam skład: A4 (595 pt), tekst 7 pt, interlinia 10 pt.

## Geometria (pomiar)

| Element | x etykiety | x tekstu / kontynuacji | Odstęp etykieta–tekst | Uwagi |
|---|---|---|---|---|
| ustęp „1.” | 39,7 | 53,9 | 7,9–10,2 pt | |
| punkt „1/” | 53,9 | 68,0 | 6,6–8,9 pt | etykieta w kolumnie tekstu rodzica |
| litera „a/” | 68,0 | 82,2 | 6,7–7,3 pt | krok poziomu ≈ 14,1 pt |
| część wspólna po wyliczeniu | — | 53,9 | — | |
| „§ N” | wyśrodkowany, x≈292–304 (z tytułem 160–435) | | | pogrubiony 9 pt, 15–25 pt odstępu nad |
| rozdział „2. Rachunki…” | 40 | | | pogrubiony 9 pt |
| słowniczek: etykieta / termin | 45,4 / 59,5 | definicja 187,1 | 8,8 pt | termin wyśrodkowany pionowo na definicji; linie poziome dzielone na 39,7–181,4 i 181,4–555,6, bez pionowych |
| akt ISAP „1)” (porównanie) | 51 | 72 | 12,6 pt | 10 pt |

## R1 — Etykiety „1/”, „a/” (US2)

**Przyczyna**: `ListLabelPatterns.Classify` zna „1)”, „1.”, „a)”, rzymskie, punktory i myślniki — „1/”, „a/” nie są
etykietami nigdzie w parserze. `TableDetectionStage.CellsOf`/`IsLabel` (l. 302–334) łączy etykietę z tekstem tylko dla
punktorów, „1)” i „a)”, więc odstęp 6–10 pt (> `MinCellGapEm` = 1 em przy 7 pt) robi z „1/” osobną komórkę; liczba
komórek w wierszach się waha (3 kolumny 39,7/53,9/68,0) → region `ambiguous` (l. 483) → tabela zastępcza + TBL001.
`IsHangingList` (l. 542) ratuje tylko regiony, w których każdy wiersz to „rozpoznana etykieta | tekst”.
`ListDetectionStage` już obsługuje kontynuacje przy wysuniętym wcięciu (także przez stronę), zagnieżdżenie po wcięciu
i randze (`Rank`, l. 294–301) oraz część wspólną (FR-054) — te wiersze do niej nie trafiają, bo mają rolę `Table`.

- **Decision**: nowe rodzaje etykiet `ArabicSlash` („1/”, „1a/”) i `LetterSlash` („a/”, „aa/”) w `ListLabelPatterns`
  (cały token 1–3 cyfry lub 1–2 małe litery + „/”, po nim tekst); dopisane wszędzie, gdzie dziś wymienione są „1)” i
  „a)”: `IsLabel` (łącznie z „1.” na granicy komórki), `KeepLabelledBoldTextInLists` (l. 191), `Rank` (`ArabicSlash` =
  2, `LetterSlash` = 3). `IsHangingList` uogólnione na „każda kolumna poza ostatnią zawiera wyłącznie etykiety”, jeśli
  replika nadal pokaże trzy kolumny.
- **Rationale**: układ jest identyczny z aktami ISAP — różni się tylko składnia etykiet; po zmianie wiersze punktów są
  jednokomórkowe i trafiają do sprawdzonej ścieżki list. Eksperyment na kopii (tylko etykiety): TBL001 132→6, 16→0,
  58→1, 79→2; wiersze z „ \| ” 1137→43, 142→0, 552→15, 665→20 (spełnia SC-080), listy „2\.” z zagnieżdżonymi „1/”.
- **Alternatives considered**: osobny etap „obszar etykieta–tekst” z adnotacją (więcej kodu, nowa adnotacja dla
  wszystkich etapów — niepotrzebne, skoro ścieżka list już działa); obniżenie progu `MinCellGapEm` (psuje prawdziwe
  tabele); przerabianie gotowych tabel zastępczych na listy (kruche, gubi zagnieżdżenie).
- **Wymagania wzorca**: „7/2017”, „13/36”, „4/49”, „Klient/Klienci”, „km/h”, samotne „i/” NIE są etykietami (testy
  wzorca); „1.” jako etykieta komórki tylko w obszarze z etykietami (ryzyko kolumny „Lp.” w taryfach).

## R2 — Paragrafy „§ N” (US1)

Role samodzielnych wierszy „§ N” po HeadingDetection (sonda): D-A 91 Table / 63 Unknown / 6 Heading (załącznik
„§ 1.”); D-B 15 / 20 / 0; D-C 51 / 70 / 0; D-D: 16 z tytułem — Table, 22 gołe „§ N.” — Heading.

| Przyczyna | Dowód | Decision | Alternatives rejected |
|---|---|---|---|
| C1: „§ 5” bez kropki nie pasuje do wzorca jednostki | `LegalUnitPatterns.cs:106` wymaga kropki (odróżnia „§ 5.” od odwołania „§ 5 ust. 2”); fallback typograficzny wymaga litery (`HeadingDetectionStage.cs:197–201`) | `TryMatch` przyjmuje także goły wiersz `^§\s*N$` (cały wiersz, bez reszty; artykuły bez zmian); HeadingDetection uznaje go tylko, gdy wiersz jest odosobniony i wyróżniony (wyśrodkowany, pogrubiony lub powiększony) i nie kontynuuje zdania | nagłówki typograficzne bez liter (numery stron, „1.”); reguła dla mBanku (FR-530) |
| C2: TableDetection zabiera wiersz przed HeadingDetection | jednokomórkowy wiersz nad ziarnem (`TableDetectionStage.cs:399–419`) albo w regionie, bo `CutAtRunningText` (660–700) tnie tylko przy lewej krawędzi | w regionach bez siatki wiersz pasujący do oznaczenia jednostki (także gołego) nigdy nie jest dołączany nad ziarnem, a region jest przed nim cięty; siatki i tabele-dokumenty bez zmian | odzyskiwanie wierszy Table w HeadingDetection (duplikacja tekstu); poleganie tylko na R1 (US1 musi być testowalny niezależnie) |
| C3: ListDetection bierze goły „§ N” za kontynuację listy | `ListDetectionStage.cs:340` zamyka listę tylko przy `entry.LegalUnit` (ten sam wzorzec) | naprawia C1 | — |
| C4: wiersz z tytułem byłby podzielony | `LegalHeading` (434–439): tekst „§ 3.” + `SplitRest` | reszta zostaje tytułem (bez podziału), gdy oznaczenie i reszta są pogrubione, tekst ciągły nie jest, a wiersz jest odosobniony lub wyśrodkowany → nagłówek „§ 3. Porady ogólne”, oznaczenie „§ 3” | — |
| C5: „§” na poziomie rozdziału zamiast pod nim | rozdziały „2. …” to nagłówki typograficzne w dokumencie prawnym; `AssignLevels` tylko obcina poziomy | w przebiegu stosu jednostka, której otwartym rodzicem jest nagłówek typograficzny pasujący do `^\d+\.\s+\p{Lu}` (numerowany rozdział), dostaje poziom rodzica + 1 | dla każdego rodzica typograficznego — zmieniłoby 4 goldeny (prawo bankowe „A. Banki państwowe”, obwieszczenie MSZ, regulamin-dwie-kolumny) |

Sprawdzenie regresji (goldeny commitowane, prywatne, `corpus/**/*.md`): nigdzie nie ma gołego „§ N”, nagłówka „§” z
tytułem, gołego rozdziału z jednostką tego samego poziomu pod numerowanym rodzicem ani jednostki w tabeli bez siatki →
oczekiwany brak zmian goldenów.

## R3 — Słowniczek (US3)

**Geometria**: zdanie wstępne x=39,7 przecina granicę kolumn (181,4); etykieta pogrubiona 45,4 („1/” w D-A, D-B; „1.” w
D-C, D-D), termin pogrubiony 59,5 (zawinięcia też 59,5), definicja 187,1, interlinia 10; termin wyśrodkowany pionowo,
definicja zaczyna się wyżej. Linie poziome tylko, każda z dwóch odcinków (39,7–181,4 i 181,4–555,6) — jedyny znacznik
kolumn; linia pod każdym wpisem, brak nad pierwszym; strona z kontynuacją zaczyna się linią. Wyliczenia w definicji:
etykieta 187,1, tekst 201,3, kolejny poziom 201,3/215,5.

**Przyczyna**: (1) brak etykiet ukośnikowych (jak R1); (2) region z liniami jest wykrywany, ale w jednym pasie są dwie
komórki („1/” osobno od terminu, „c/” osobno od tekstu) → `ambiguous` (483–484) → `AddVisualRow` wierszami w kolejności
y (494–497) → przeplot terminu z definicją; (3) wiersze nad pierwszą linią są odcinane od siatki (378–383).

Eksperyment (tylko etykiety): słowniczki D-A, D-B stają się dwukolumnowymi tabelami GFM poprawnie sparowanymi (poza
pierwszym wpisem, wyliczenia spłaszczone); D-C, D-D nadal w trybie zastępczym.

- **Decision**: w TableDetection region z ≥ 2 liniami poziomymi dzielonymi na wspólnym x, bez pionowych, w którym lewa
  strona każdego wpisu to etykieta + pogrubiony termin, jest **listą definicji**, nie tabelą: wpis = pas między
  kolejnymi liniami (pierwszy — od zdania wstępnego przecinającego granicę do pierwszej linii; strona bez górnej linii
  kontynuuje poprzedni wpis); lewe wiersze → etykieta + termin (zawinięcia złączone), prawe w kolejności y →
  definicja. TableDetection oznacza wiersze adnotacjami (`deflist.entry`, `deflist.side`) zamiast tworzyć `Table`;
  ListDetection buduje element `- 1/ **termin** definicja…` z wyliczeniami definicji zagnieżdżonymi (logika R1).
- **Rationale**: granice wpisów dają tylko linie; adnotacje to ustalony sposób komunikacji etapów (jak `tabledoc.*`).
- **Alternatives considered**: tabela GFM (odrzucona przez właściciela, opcja C; spłaszcza wyliczenia);
  rozszerzenie tabeli-dokumentu z 002 (wymaga pełnej siatki, ≥ 50% stron, lewa komórka → nagłówek); przestawianie
  wierszy po środku pionowym (kruche, i tak potrzebuje granic wpisów).
- **Ryzyka**: łączenie etykiet „1.” z terminem w pierwszej kolumnie taryf/procedur („Lp.”) — warunek „pogrubiony
  termin + linie dzielone na wspólnym x + brak pionowych”; słowniczek bez zdania wstępnego — granicą pierwszego wpisu
  poprzedzający nagłówek.

## R4 — Pomiar i prywatny korpus

- **Decision**: miary SC-080…SC-087 liczy pomocnik testowy w `LegalAgent.PdfParser.Tests` (bez Pythona), uruchamiany
  w teście prywatnego korpusu (`LEGALAGENT_PRIVATE_CORPUS`) i drukujący tabelę miar przy `LEGALAGENT_CORPUS_REPORT`:
  TBL001 z raportu, wiersze z „ \| ”, wiersze zaczynające się etykietą bez listy, gołe „§ N” poza nagłówkami, nagłówki
  spisu treści, numery „N/M”. Progi SC-080 sprawdzane dla dokumentów oznaczonych w pliku prywatnym jako „układ
  etykiet” (lista nazw w nieśledzonym pliku obok PDF-ów — biblioteka i testy commitowane nie znają nazw).
- **Rationale**: jeden powtarzalny pomiar w tym samym języku co testy; prawdziwe dokumenty i ich nazwy zostają poza
  git (FR-533).
- **Alternatives considered**: skrypt Python ze scratchpada (poza solucją, nie przechodzi przez CI ani przegląd);
  ręczne liczenie (niepowtarzalne).
- **Prywatny korpus**: 15 dokumentów mBanku dodanych jako przypadki z goldenami **obecnego** wyniku (punkt
  odniesienia) przed pierwszą zmianą parsera; zmiany goldenów D-A…D-D oczekiwane, 5 detalicznych — do akceptacji
  właściciela (SC-085), 6 pozostałych dla firm — miary nie gorsze.

## R5 — Kolejność i ryzyko harmonogramu

- **Decision**: US2 (etykiety) przed US1 (§), bo usuwa tabele zastępcze, które pochłaniają wiersze „§” (C2 w dużej
  części), i daje największy efekt mierzalny; potem US1 (C1, C3–C5, osobno C2), US3, US5 opcjonalnie.
- **Rationale**: eksperyment R1 już spełnia SC-080 przy małej zmianie; ryzyko regresji skupione w jednym pliku wzorców
  i kilku miejscach TableDetection.
- **Alternatives considered**: US1 najpierw (zgodnie z priorytetem w spec) — C2 wymagałoby osobnej osłony w regionach,
  które po US2 przestają być tabelami.
